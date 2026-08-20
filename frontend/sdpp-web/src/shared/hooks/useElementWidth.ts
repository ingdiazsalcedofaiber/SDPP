import { useEffect, useState } from "react";
import type { RefObject } from "react";

/** Tracks a DOM element's actual rendered width via ResizeObserver — used by the PDF viewers
 * (EnvelopeEditorPage, EnvelopeSigningPage, QuickSignPage) to size react-pdf's <Page> to whatever
 * room is really available instead of a fixed pixel width that overflows a phone screen. react-pdf
 * needs a real pixel number for width (no percentage support), so there's no pure-CSS way to make
 * the canvas responsive — this is the one JS measurement that makes it possible. */
export function useElementWidth<T extends HTMLElement>(ref: RefObject<T | null>): number | null {
  const [width, setWidth] = useState<number | null>(null);

  useEffect(() => {
    const element = ref.current;
    if (!element) return;

    const observer = new ResizeObserver((entries) => {
      const entry = entries[0];
      if (entry) setWidth(entry.contentRect.width);
    });
    observer.observe(element);
    setWidth(element.getBoundingClientRect().width);

    return () => observer.disconnect();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [ref.current]);

  return width;
}
