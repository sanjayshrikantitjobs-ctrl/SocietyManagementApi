/** Mirrors SocietyManagement.Shared.Wrappers.ApiResponse<T> — every API call
 * returns this envelope so HTTP interceptors and services can unwrap `data`
 * and surface `message`/`errors` uniformly. */
export interface ApiResponse<T> {
  success: boolean;
  message: string;
  data: T | null;
  errors: string[] | null;
}

/** Mirrors SocietyManagement.Shared.Wrappers.PaginatedResult<T>. */
export interface PaginatedResult<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}
