/** Mirrors the backend's shared `PagedResult<T>` envelope (ADR-0002 projections). */
export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}
