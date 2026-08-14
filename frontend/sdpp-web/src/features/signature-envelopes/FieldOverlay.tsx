import { useRef, useState } from "react";
import Box from "@mui/material/Box";
import IconButton from "@mui/material/IconButton";
import Typography from "@mui/material/Typography";
import CloseIcon from "@mui/icons-material/Close";
import type { FieldType } from "../../shared/api/signature";
import { FIELD_TYPE_LABELS } from "../../shared/api/signature";

export interface FieldRect {
  positionX: number;
  positionY: number;
  width: number;
  height: number;
}

const clamp = (value: number, min: number, max: number) => Math.min(max, Math.max(min, value));

/**
 * One placed field, editable — same "recompute absolute position from a freshly-measured page
 * rect on every pointer event, never accumulate deltas on cached state" discipline as the
 * original single-signer PlacementOverlay (see its own doc comment for why that mattered:
 * "la firma no queda donde la coloco"). Unlike that component, fields have no fixed aspect ratio
 * to preserve — free resize from the bottom-right handle. Position/size only persist to the
 * server (onCommit) once, on pointer release, not on every pointermove — dragging updates local
 * state for live visual feedback instead.
 */
export function EditableFieldBox({
  pageContainerRef,
  field,
  color,
  label,
  selected,
  onSelect,
  onCommit,
  onDelete,
}: {
  pageContainerRef: React.RefObject<HTMLDivElement | null>;
  field: FieldRect & { fieldId: string; type: FieldType; required: boolean };
  color: string;
  label: string;
  selected?: boolean;
  onSelect?: (fieldId: string) => void;
  onCommit: (fieldId: string, rect: FieldRect) => void;
  onDelete: (fieldId: string) => void;
}) {
  const [liveRect, setLiveRect] = useState<FieldRect | null>(null);
  const dragState = useRef<
    | { mode: "move"; grabDxFraction: number; grabDyFraction: number; pointerId: number }
    | { mode: "resize"; anchorXFraction: number; anchorYFraction: number; pointerId: number }
    | null
  >(null);

  const rect = liveRect ?? field;
  const getPageRect = (): DOMRect | null => pageContainerRef.current?.getBoundingClientRect() ?? null;

  const startMove = (e: React.PointerEvent) => {
    const pageRect = getPageRect();
    if (!pageRect) return;
    e.stopPropagation();
    onSelect?.(field.fieldId);
    e.currentTarget.setPointerCapture(e.pointerId);
    const pointerXFraction = (e.clientX - pageRect.left) / pageRect.width;
    const pointerYFraction = (e.clientY - pageRect.top) / pageRect.height;
    dragState.current = {
      mode: "move",
      grabDxFraction: pointerXFraction - rect.positionX,
      grabDyFraction: pointerYFraction - rect.positionY,
      pointerId: e.pointerId,
    };
  };

  const startResize = (e: React.PointerEvent) => {
    const pageRect = getPageRect();
    if (!pageRect) return;
    e.stopPropagation();
    e.currentTarget.setPointerCapture(e.pointerId);
    dragState.current = { mode: "resize", anchorXFraction: rect.positionX, anchorYFraction: rect.positionY, pointerId: e.pointerId };
  };

  const handlePointerMove = (e: React.PointerEvent) => {
    const state = dragState.current;
    const pageRect = getPageRect();
    if (!state || !pageRect || e.pointerId !== state.pointerId) return;

    const pointerXFraction = (e.clientX - pageRect.left) / pageRect.width;
    const pointerYFraction = (e.clientY - pageRect.top) / pageRect.height;

    if (state.mode === "move") {
      const positionX = clamp(pointerXFraction - state.grabDxFraction, 0, 1 - rect.width);
      const positionY = clamp(pointerYFraction - state.grabDyFraction, 0, 1 - rect.height);
      setLiveRect({ ...rect, positionX, positionY });
    } else {
      const width = clamp(pointerXFraction - state.anchorXFraction, 0.03, 1 - state.anchorXFraction);
      const height = clamp(pointerYFraction - state.anchorYFraction, 0.02, 1 - state.anchorYFraction);
      setLiveRect({ positionX: state.anchorXFraction, positionY: state.anchorYFraction, width, height });
    }
  };

  const endDrag = () => {
    dragState.current = null;
    if (liveRect) {
      onCommit(field.fieldId, liveRect);
    }
  };

  return (
    <Box
      onPointerMove={handlePointerMove}
      onPointerUp={endDrag}
      onPointerCancel={endDrag}
      onPointerDown={startMove}
      sx={{
        position: "absolute",
        // Above NewFieldPlacementCatcher's zIndex:5 — that catcher stays mounted (and covers the
        // whole page) for as long as a field type is armed, so multiple fields of the same type
        // can be placed without re-clicking the chip each time. Without outranking it here, its
        // full-page transparent layer swallows every pointer event meant for an already-placed
        // field — including drags — since it sits later... no, EARLIER in z-order (explicit
        // z-index beats DOM order for stacking), even though it's declared first in the JSX.
        zIndex: 10,
        left: `${rect.positionX * 100}%`,
        top: `${rect.positionY * 100}%`,
        width: `${rect.width * 100}%`,
        height: `${rect.height * 100}%`,
        border: selected ? `3px dashed ${color}` : `2px solid ${color}`,
        boxShadow: selected ? `0 0 0 2px ${color}55` : "none",
        borderRadius: 1,
        bgcolor: `${color}22`,
        cursor: "move",
        touchAction: "none",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        overflow: "hidden",
      }}
    >
      <Typography variant="caption" sx={{ color, fontWeight: 700, px: 0.5, textAlign: "center", pointerEvents: "none" }}>
        {FIELD_TYPE_LABELS[field.type]}
        {field.required ? " *" : ""}
        <br />
        {label}
      </Typography>

      <IconButton
        size="small"
        onPointerDown={(e) => e.stopPropagation()}
        onClick={() => onDelete(field.fieldId)}
        sx={{ position: "absolute", top: -12, right: -12, bgcolor: "#fff", boxShadow: 1, width: 22, height: 22, "&:hover": { bgcolor: "#fff" } }}
      >
        <CloseIcon sx={{ fontSize: 14, color: "#B23A2E" }} />
      </IconButton>

      <Box
        onPointerDown={startResize}
        sx={{
          position: "absolute",
          right: -8,
          bottom: -8,
          width: 18,
          height: 18,
          borderRadius: "50%",
          bgcolor: color,
          border: "2px solid #fff",
          boxShadow: 1,
          cursor: "nwse-resize",
          touchAction: "none",
        }}
      />
    </Box>
  );
}

/**
 * Transparent full-page hit layer for placing a brand-new field: single click (not drag) drops a
 * field of the currently-armed type at that point, with a sensible default size per type — the
 * user then drags/resizes it via EditableFieldBox afterward if needed. Only active while a type is
 * armed (see EnvelopeEditorPage's "toolbar"), so it never intercepts clicks otherwise.
 */
export function NewFieldPlacementCatcher({
  pageContainerRef,
  armedType,
  onPlace,
}: {
  pageContainerRef: React.RefObject<HTMLDivElement | null>;
  armedType: FieldType | null;
  onPlace: (rect: FieldRect) => void;
}) {
  if (!armedType) return null;

  const defaultSize: Record<FieldType, { width: number; height: number }> = {
    Signature: { width: 0.22, height: 0.07 },
    Initials: { width: 0.1, height: 0.05 },
    Stamp: { width: 0.15, height: 0.09 },
    // Matches sello.png's real aspect ratio (111×67 ≈ 1.66:1) at a compact, corner-friendly
    // default — this same rect repeats identically on every page (see EnvelopeEditorPage's info
    // alert), so a smaller default footprint is less likely to land on running text on some page.
    LegalApprovalStamp: { width: 0.12, height: 0.0723 },
    Date: { width: 0.14, height: 0.03 },
    Name: { width: 0.18, height: 0.03 },
    Title: { width: 0.18, height: 0.03 },
    Text: { width: 0.2, height: 0.03 },
    Checkbox: { width: 0.03, height: 0.03 },
  };

  const handleClick = (e: React.MouseEvent) => {
    const pageRect = pageContainerRef.current?.getBoundingClientRect();
    if (!pageRect) return;
    const { width, height } = defaultSize[armedType];
    const centerXFraction = (e.clientX - pageRect.left) / pageRect.width;
    const centerYFraction = (e.clientY - pageRect.top) / pageRect.height;
    onPlace({
      positionX: clamp(centerXFraction - width / 2, 0, 1 - width),
      positionY: clamp(centerYFraction - height / 2, 0, 1 - height),
      width,
      height,
    });
  };

  return (
    <Box
      onClick={handleClick}
      sx={{ position: "absolute", inset: 0, cursor: "crosshair", zIndex: 5 }}
    />
  );
}
