import { create } from "zustand";
import type { OperationType } from "../../shared/api/types";

type Updater<T> = T | ((prev: T) => T);

function resolve<T>(value: Updater<T>, prev: T): T {
  return typeof value === "function" ? (value as (prev: T) => T)(prev) : value;
}

interface ConversionWizardState {
  activeStep: number;
  file: File | null;
  additionalFiles: File[];
  operationType: OperationType;
  operationParams: Record<string, string>;
  documentId: string | null;
  jobId: string | null;
  setActiveStep: (value: number) => void;
  setFile: (value: File | null) => void;
  setAdditionalFiles: (value: Updater<File[]>) => void;
  setOperationType: (value: OperationType) => void;
  setOperationParams: (value: Updater<Record<string, string>>) => void;
  setDocumentId: (value: string | null) => void;
  setJobId: (value: string | null) => void;
  reset: () => void;
}

const initialState = {
  activeStep: 0,
  file: null as File | null,
  additionalFiles: [] as File[],
  operationType: "WordToPdf" as OperationType,
  operationParams: {} as Record<string, string>,
  documentId: null as string | null,
  jobId: null as string | null,
};

/**
 * Holds the entire "Nueva conversión" wizard (selected operation, uploaded file, job status)
 * outside the page component. `ConvertWizardPage` unmounts every time the user visits another
 * section (Dashboard, Auditoría, ...) — anything kept in local useState was silently lost on the
 * way back, which is exactly the "se pierde lo que estoy haciendo" complaint this fixes. Only
 * resets explicitly: via `reset()` (the "Convertir otro documento" button once a job reaches a
 * terminal status) or on logout (see app/auth.ts).
 */
export const useConversionWizardStore = create<ConversionWizardState>((set) => ({
  ...initialState,
  setActiveStep: (value) => set({ activeStep: value }),
  setFile: (value) => set({ file: value }),
  setAdditionalFiles: (value) => set((state) => ({ additionalFiles: resolve(value, state.additionalFiles) })),
  setOperationType: (value) => set({ operationType: value }),
  setOperationParams: (value) => set((state) => ({ operationParams: resolve(value, state.operationParams) })),
  setDocumentId: (value) => set({ documentId: value }),
  setJobId: (value) => set({ jobId: value }),
  reset: () => set({ ...initialState }),
}));
