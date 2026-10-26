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

// Access token lives only in memory (never in localStorage/sessionStorage).
let accessToken: string | null = null

export const tokenStore = {
  get: () => accessToken,
  set: (token: string | null) => {
    accessToken = token
  },
}

interface SessionHandlers {
  /** Obtains a new access token via the refresh cookie; resolves to false when the session is gone. */
  refresh: () => Promise<boolean>
  /** Called when a request is still unauthorized after a refresh attempt. */
  onExpired: () => void
}

let sessionHandlers: SessionHandlers = {
  refresh: async () => false,
  onExpired: () => {},
}

export function setSessionHandlers(handlers: SessionHandlers) {
  sessionHandlers = handlers
}

let refreshInFlight: Promise<boolean> | null = null

/**
 * Single-flight refresh: concurrent callers share one /auth/refresh request. The backend rotates
 * refresh tokens and treats a replayed token as theft, so parallel refreshes must never happen.
 */
export function refreshSession(): Promise<boolean> {
  refreshInFlight ??= sessionHandlers
    .refresh()
    .catch(() => false)
    .finally(() => {
      refreshInFlight = null
    })
  return refreshInFlight
}

// Auth endpoints answer 401 for bad credentials or an expired refresh cookie; never refresh for them.
const isAuthEndpoint = (path: string) => path.startsWith('/auth/')

type Method = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'

async function request<T>(
  method: Method,
  path: string,
  body?: unknown,
  retried = false,
): Promise<T> {
  const headers = new Headers({ Accept: 'application/json, text/plain' })
  // FormData (file uploads) sets its own multipart content type with the boundary.
  const isForm = body instanceof FormData
  if (body !== undefined && !isForm) {
    headers.set('Content-Type', 'application/json')
  }
  const token = tokenStore.get()
  if (token) {
    headers.set('Authorization', `Bearer ${token}`)
  }

  let response: Response
  try {
    response = await fetch(`${BASE_URL}${path}`, {
      method,
      headers,
      body: body === undefined ? undefined : isForm ? body : JSON.stringify(body),
    })
  } catch {
    throw new ApiError(0, { title: 'Sunucuya ulaşılamadı.' })
  }

  if (response.status === 401 && !isAuthEndpoint(path)) {
    if (!retried && (await refreshSession())) {
      return request<T>(method, path, body, true)
    }
    sessionHandlers.onExpired()
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

export function isApiError(error: unknown, status?: number): error is ApiError {
  return error instanceof ApiError && (status === undefined || error.status === status)
}

export function getErrorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    return error.message
  }
  return 'Beklenmeyen bir hata oluştu.'
}
