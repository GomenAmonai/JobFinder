export interface SavedFilterDto {
  id: number;
  name: string;
  market: string | null;
  level: string | null;
  stack: string | null;
  q: string | null;
  version: string;
  createdAt: string;
  updatedAt: string;
}

export interface SaveFilterInput {
  name: string;
  market?: string;
  level?: string;
  stack?: string;
  q?: string;
}
