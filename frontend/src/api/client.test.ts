import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError, apiClient, getErrorMessage, setSessionHandlers, tokenStore } from '@/api/client'

function jsonResponse(status: number, body: unknown, contentType = 'application/json') {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': contentType } })
}

describe('apiClient', () => {
  const fetchMock = vi.fn<typeof fetch>()

  beforeEach(() => {
    vi.stubGlobal('fetch', fetchMock)
  })

  afterEach(() => {
    fetchMock.mockReset()
    vi.unstubAllGlobals()
    tokenStore.set(null)
    setSessionHandlers({ refresh: async () => false, onExpired: () => {} })
  })

  function useSession(refresh: () => Promise<boolean>) {
    const onExpired = vi.fn()
    setSessionHandlers({ refresh, onExpired })
    return { onExpired }
  }

  it('prefixes requests with /api and parses JSON', async () => {
    fetchMock.mockResolvedValue(jsonResponse(200, { id: 1 }))

    const result = await apiClient.get<{ id: number }>('/products/1')

    expect(result).toEqual({ id: 1 })
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/products/1',
      expect.objectContaining({ method: 'GET' }),
    )
  })

  it('returns plain text bodies as strings', async () => {
    fetchMock.mockResolvedValue(
      new Response('pong', { status: 200, headers: { 'Content-Type': 'text/plain' } }),
    )

    await expect(apiClient.get<string>('/ping')).resolves.toBe('pong')
  })

  it('returns undefined for empty responses', async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 204 }))

    await expect(apiClient.delete('/products/1')).resolves.toBeUndefined()
  })

  it('sends JSON bodies', async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 204 }))

    await apiClient.post('/products', { name: 'Matkap' })

    const init = fetchMock.mock.calls[0][1]!
    expect(init.body).toBe('{"name":"Matkap"}')
    expect(new Headers(init.headers).get('Content-Type')).toBe('application/json')
  })

  it('throws ApiError with parsed problem details', async () => {
    fetchMock.mockResolvedValue(
      jsonResponse(
        422,
        {
          title: 'Business rule violated.',
          detail: 'Takip tipi değiştirilemez.',
          status: 422,
          code: 'Product.TrackingTypeLocked',
          traceId: '00-abc-01',
        },
        'application/problem+json',
      ),
    )

    const error = await apiClient.get('/products/1').catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    const apiError = error as ApiError
    expect(apiError.status).toBe(422)
    expect(apiError.code).toBe('Product.TrackingTypeLocked')
    expect(apiError.traceId).toBe('00-abc-01')
    expect(apiError.message).toBe('Takip tipi değiştirilemez.')
  })

  it('exposes validation field errors', async () => {
    fetchMock.mockResolvedValue(
      jsonResponse(
        400,
        { title: 'Validation failed.', status: 400, errors: { Name: ['Ad zorunludur.'] } },
        'application/problem+json',
      ),
    )

    const error = (await apiClient.post('/products', {}).catch((e: unknown) => e)) as ApiError

    expect(error.errors).toEqual({ Name: ['Ad zorunludur.'] })
  })

  it('falls back to status text when the error body is not JSON', async () => {
    fetchMock.mockResolvedValue(new Response('oops', { status: 502, statusText: 'Bad Gateway' }))

    const error = (await apiClient.get('/ping').catch((e: unknown) => e)) as ApiError

    expect(error.status).toBe(502)
    expect(error.message).toBe('Bad Gateway')
  })

  it('wraps network failures in ApiError with status 0', async () => {
    fetchMock.mockRejectedValue(new TypeError('Failed to fetch'))

    const error = (await apiClient.get('/ping').catch((e: unknown) => e)) as ApiError

    expect(error.status).toBe(0)
    expect(getErrorMessage(error)).toBe('Sunucuya ulaşılamadı.')
  })

  it('retries once after a successful refresh on 401', async () => {
    const refresh = vi.fn(async () => true)
    useSession(refresh)
    fetchMock
      .mockResolvedValueOnce(new Response(null, { status: 401 }))
      .mockResolvedValueOnce(jsonResponse(200, { ok: true }))

    await expect(apiClient.get('/me')).resolves.toEqual({ ok: true })
    expect(refresh).toHaveBeenCalledOnce()
    expect(fetchMock).toHaveBeenCalledTimes(2)
  })

  it('does not retry and reports an expired session when refresh fails', async () => {
    const { onExpired } = useSession(async () => false)
    fetchMock.mockResolvedValue(new Response(null, { status: 401, statusText: 'Unauthorized' }))

    const error = (await apiClient.get('/me').catch((e: unknown) => e)) as ApiError

    expect(error.status).toBe(401)
    expect(fetchMock).toHaveBeenCalledOnce()
    expect(onExpired).toHaveBeenCalledOnce()
  })

  it('sends the in-memory access token as a bearer token', async () => {
    tokenStore.set('abc')
    fetchMock.mockResolvedValue(new Response(null, { status: 204 }))

    await apiClient.get('/items')

    expect(new Headers(fetchMock.mock.calls[0][1]!.headers).get('Authorization')).toBe('Bearer abc')
  })

  it('resolves concurrent 401s with a single refresh request', async () => {
    let releaseRefresh!: (value: boolean) => void
    const refresh = vi.fn(
      () =>
        new Promise<boolean>((resolve) => {
          releaseRefresh = resolve
        }),
    )
    useSession(refresh)
    const attempts = new Map<string, number>()
    fetchMock.mockImplementation(async (input) => {
      const url = String(input)
      const attempt = (attempts.get(url) ?? 0) + 1
      attempts.set(url, attempt)
      return attempt === 1 ? new Response(null, { status: 401 }) : jsonResponse(200, { url })
    })

    const requests = Promise.all([apiClient.get('/a'), apiClient.get('/b'), apiClient.get('/c')])
    await vi.waitFor(() => expect(refresh).toHaveBeenCalled())
    releaseRefresh(true)

    await expect(requests).resolves.toEqual([
      { url: '/api/a' },
      { url: '/api/b' },
      { url: '/api/c' },
    ])
    expect(refresh).toHaveBeenCalledOnce()
    expect(fetchMock).toHaveBeenCalledTimes(6)
  })

  it('never refreshes for auth endpoints', async () => {
    const refresh = vi.fn(async () => true)
    const { onExpired } = useSession(refresh)
    fetchMock.mockResolvedValue(jsonResponse(401, { detail: 'E-posta veya şifre hatalı.' }))

    const error = (await apiClient.post('/auth/login', {}).catch((e: unknown) => e)) as ApiError

    expect(error.message).toBe('E-posta veya şifre hatalı.')
    expect(refresh).not.toHaveBeenCalled()
    expect(onExpired).not.toHaveBeenCalled()
  })

  it('does not loop when the retried request is also 401', async () => {
    useSession(async () => true)
    fetchMock.mockImplementation(async () => new Response(null, { status: 401 }))

    const error = (await apiClient.get('/me').catch((e: unknown) => e)) as ApiError

    expect(error.status).toBe(401)
    expect(fetchMock).toHaveBeenCalledTimes(2)
  })
})

describe('getErrorMessage', () => {
  it('returns a generic message for unknown errors', () => {
    expect(getErrorMessage(new Error('internal'))).toBe('Beklenmeyen bir hata oluştu.')
  })
})
