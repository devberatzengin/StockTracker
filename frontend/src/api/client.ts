/** RFC 9457 problem details as returned by the backend's global exception handler. */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  traceId?: string
  /** Domain error code, present on 422 responses (e.g. `Product.TrackingTypeLocked`). */
  code?: string
  /** Field errors, present on 400 validation responses. */
  errors?: Record<string, string[]>
}

export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails

  constructor(status: number, problem: ProblemDetails) {
    super(problem.detail ?? problem.title ?? `İstek başarısız oldu (${status}).`)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }

  get code() {
    return this.problem.code
  }

  get errors() {
    return this.problem.errors
  }

  get traceId() {
    return this.problem.traceId
  }
}

const BASE_URL = '/api'

type RefreshHandler = () => Promise<boolean>

// Filled in Phase 1.1: calls /auth/refresh and reports whether a new access token was obtained.
let refreshHandler: RefreshHandler = async () => false

export function setRefreshHandler(handler: RefreshHandler) {
  refreshHandler = handler
}

type Method = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'

async function request<T>(
  method: Method,
  path: string,
  body?: unknown,
  retried = false,
): Promise<T> {
  const headers = new Headers({ Accept: 'application/json, text/plain' })
  if (body !== undefined) {
    headers.set('Content-Type', 'application/json')
  }

  let response: Response
  try {
    response = await fetch(`${BASE_URL}${path}`, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  } catch {
    throw new ApiError(0, { title: 'Sunucuya ulaşılamadı.' })
  }

  if (response.status === 401 && !retried && (await refreshHandler())) {
    return request<T>(method, path, body, true)
  }

  if (!response.ok) {
    throw new ApiError(response.status, await readProblem(response))
  }

  return (await readBody(response)) as T
}

async function readBody(response: Response): Promise<unknown> {
  const text = await response.text()
  if (text.length === 0) {
    return undefined
  }
  return isJson(response) ? JSON.parse(text) : text
}

async function readProblem(response: Response): Promise<ProblemDetails> {
  const fallback: ProblemDetails = {
    status: response.status,
    title: response.statusText || undefined,
  }
  if (!isJson(response)) {
    return fallback
  }
  try {
    return { ...fallback, ...((await response.json()) as ProblemDetails) }
  } catch {
    return fallback
  }
}

function isJson(response: Response) {
  return response.headers.get('Content-Type')?.includes('json') ?? false
}

export const apiClient = {
  get: <T>(path: string) => request<T>('GET', path),
  post: <T>(path: string, body?: unknown) => request<T>('POST', path, body),
  put: <T>(path: string, body?: unknown) => request<T>('PUT', path, body),
  patch: <T>(path: string, body?: unknown) => request<T>('PATCH', path, body),
  delete: <T = void>(path: string) => request<T>('DELETE', path),
}

export function getErrorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    return error.message
  }
  return 'Beklenmeyen bir hata oluştu.'
}
