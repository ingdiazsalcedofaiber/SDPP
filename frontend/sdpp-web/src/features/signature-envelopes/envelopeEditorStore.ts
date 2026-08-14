import { create } from "zustand";
import type { DispatchedRecipient, FieldType, SigningMode } from "../../shared/api/signature";

export interface EditorRecipient {
  recipientId: string;
  email: string;
  fullName: string;
  order: number;
}

export interface EditorField {
  fieldId: string;
  recipientId: string;
  type: FieldType;
  pageNumber: number;
  positionX: number;
  positionY: number;
  width: number;
  height: number;
  required: boolean;
}

interface EnvelopeEditorState {
  activeStep: number;
  file: File | null;
  pdfBlob: Blob | null;
  sourceDocumentId: string | null;
  envelopeId: string | null;
  title: string;
  message: string;
  signingMode: SigningMode;
  dueDateUtc: string | null;
  recipients: EditorRecipient[];
  fields: EditorField[];
  armedFieldType: FieldType | null;
  activeRecipientId: string | null;
  dispatched: DispatchedRecipient[] | null;
  setActiveStep: (value: number) => void;
  setFile: (value: File | null) => void;
  setPdfBlob: (value: Blob | null) => void;
  setSourceDocumentId: (value: string | null) => void;
  setEnvelopeId: (value: string | null) => void;
  setTitle: (value: string) => void;
  setMessage: (value: string) => void;
  setSigningMode: (value: SigningMode) => void;
  setDueDateUtc: (value: string | null) => void;
  addRecipientLocal: (recipient: EditorRecipient) => void;
  removeRecipientLocal: (recipientId: string) => void;
  addFieldLocal: (field: EditorField) => void;
  updateFieldLocal: (fieldId: string, rect: { positionX: number; positionY: number; width: number; height: number }) => void;
  removeFieldLocal: (fieldId: string) => void;
  setArmedFieldType: (value: FieldType | null) => void;
  setActiveRecipientId: (value: string | null) => void;
  setDispatched: (value: DispatchedRecipient[] | null) => void;
  reset: () => void;
}

const initialState = {
  activeStep: 0,
  file: null as File | null,
  pdfBlob: null as Blob | null,
  sourceDocumentId: null as string | null,
  envelopeId: null as string | null,
  title: "",
  message: "",
  signingMode: "Sequential" as SigningMode,
  dueDateUtc: null as string | null,
  recipients: [] as EditorRecipient[],
  fields: [] as EditorField[],
  armedFieldType: null as FieldType | null,
  activeRecipientId: null as string | null,
  dispatched: null as DispatchedRecipient[] | null,
};

/** Holds the whole "Nuevo sobre de firma" editor state outside the page component — same reason
 * as the conversion/signature wizards: the page unmounts on every route change. Recipients/fields
 * are added optimistically to local state right after each successful server call (the server is
 * the source of truth for ids, but round-tripping a full re-fetch after every click would make the
 * editor feel sluggish). */
export const useEnvelopeEditorStore = create<EnvelopeEditorState>((set) => ({
  ...initialState,
  setActiveStep: (value) => set({ activeStep: value }),
  setFile: (value) => set({ file: value }),
  setPdfBlob: (value) => set({ pdfBlob: value }),
  setSourceDocumentId: (value) => set({ sourceDocumentId: value }),
  setEnvelopeId: (value) => set({ envelopeId: value }),
  setTitle: (value) => set({ title: value }),
  setMessage: (value) => set({ message: value }),
  setSigningMode: (value) => set({ signingMode: value }),
  setDueDateUtc: (value) => set({ dueDateUtc: value }),
  addRecipientLocal: (recipient) => set((s) => ({ recipients: [...s.recipients, recipient] })),
  removeRecipientLocal: (recipientId) =>
    set((s) => ({
      recipients: s.recipients.filter((r) => r.recipientId !== recipientId),
      fields: s.fields.filter((f) => f.recipientId !== recipientId),
    })),
  addFieldLocal: (field) => set((s) => ({ fields: [...s.fields, field] })),
  updateFieldLocal: (fieldId, rect) =>
    set((s) => ({ fields: s.fields.map((f) => (f.fieldId === fieldId ? { ...f, ...rect } : f)) })),
  removeFieldLocal: (fieldId) => set((s) => ({ fields: s.fields.filter((f) => f.fieldId !== fieldId) })),
  setArmedFieldType: (value) => set({ armedFieldType: value }),
  setActiveRecipientId: (value) => set({ activeRecipientId: value }),
  setDispatched: (value) => set({ dispatched: value }),
  reset: () => set({ ...initialState }),
}));
