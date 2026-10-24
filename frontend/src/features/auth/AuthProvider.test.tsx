import { useQuery } from '@tanstack/react-query'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Outlet } from 'react-router'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { apiClient, tokenStore } from '@/api/client'
import { AuthRoot } from '@/features/auth/AuthRoot'
import { useAuth } from '@/features/auth/authContext'
import { LoginPage } from '@/features/auth/LoginPage'
import { ProtectedRoute } from '@/features/auth/ProtectedRoute'
import { empty, fakeUser, json, mockApi, renderRoutes } from '@/test/utils'

function DataProbe({ path }: { path: string }) {
  const { data } = useQuery({
    queryKey: [path],
    queryFn: () => apiClient.get<{ value: string }>(path),
  })
  return <p>{data ? `${path}: ${data.value}` : `${path}: yükleniyor`}</p>
}

function SecretPage() {
  const { user } = useAuth()
  return (
    <>
      <h1>Gizli sayfa</h1>
      <p>Hoş geldin {user?.fullName}</p>
      <DataProbe path="/stock" />
      <DataProbe path="/locations" />
    </>
  )
}

const routes = [
  {
    element: <AuthRoot />,
    children: [
      { path: '/login', element: <LoginPage /> },
      {
        element: <ProtectedRoute />,
        children: [
          { element: <Outlet />, children: [{ path: '/secret', element: <SecretPage /> }] },
        ],
      },
    ],
  },
]

/** Fake backend: issues numbered tokens and accepts only the latest one. */
function createBackend({ sessionValid = true } = {}) {
  let issued = 0
  let valid = sessionValid
  const refreshCalls = vi.fn()
  const current = () => `token-${issued}`
  const authorized = (request: Request) =>
    request.headers.get('Authorization') === `Bearer ${current()}`
  const data = (value: string) => (request: Request) =>
    authorized(request) ? json(200, { value }) : empty(401)

  const fetchMock = mockApi({
    'POST /api/auth/refresh': () => {
      refreshCalls()
      if (!valid) return empty(401)
      issued++
      return json(200, { accessToken: current(), expiresAt: '2030-01-01T00:00:00Z' })
    },
    'POST /api/auth/login': async (request) => {
      const body = (await request.json()) as { password: string }
      if (body.password !== 'Sifre1234') return json(401, { detail: 'E-posta veya şifre hatalı.' })
      valid = true
      issued++
      return json(200, { accessToken: current(), expiresAt: '2030-01-01T00:00:00Z' })
    },
    'GET /api/auth/me': (request) => (authorized(request) ? json(200, fakeUser()) : empty(401)),
    'GET /api/stock': data('42'),
    'GET /api/locations': data('A1'),
  })

  return {
    fetchMock,
    refreshCalls,
    /** Simulates the access token expiring on the server side. */
    expireAccessToken: () => {
      issued++
    },
    /** Simulates the refresh cookie becoming invalid. */
    endSession: () => {
      valid = false
    },
  }
}

describe('AuthProvider', () => {
  beforeEach(() => {
    tokenStore.set(null)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('signs in silently on startup with the refresh cookie', async () => {
    const backend = createBackend()
    vi.stubGlobal('fetch', backend.fetchMock)

    renderRoutes(routes, '/secret')

    expect(await screen.findByText('Hoş geldin Ayşe Yılmaz')).toBeInTheDocument()
    expect(await screen.findByText('/stock: 42')).toBeInTheDocument()
    expect(backend.refreshCalls).toHaveBeenCalledOnce()
  })

  it('refreshes once on concurrent 401s and retries the requests', async () => {
    const backend = createBackend()
    vi.stubGlobal('fetch', backend.fetchMock)
    const { router } = renderRoutes(routes, '/login')
    await waitFor(() => expect(router.state.location.pathname).toBe('/'))

    // The token issued at startup is no longer accepted when the page loads its data.
    backend.expireAccessToken()
    await router.navigate('/secret')

    expect(await screen.findByText('/stock: 42')).toBeInTheDocument()
    expect(await screen.findByText('/locations: A1')).toBeInTheDocument()
    // One silent sign-in on startup plus a single shared refresh for both 401s.
    expect(backend.refreshCalls).toHaveBeenCalledTimes(2)
    expect(tokenStore.get()).toBe('token-3')
  })

  it('redirects to the login page when the refresh fails', async () => {
    const backend = createBackend()
    vi.stubGlobal('fetch', backend.fetchMock)
    const { router } = renderRoutes(routes, '/login')
    await waitFor(() => expect(router.state.location.pathname).toBe('/'))

    backend.expireAccessToken()
    backend.endSession()
    await router.navigate('/secret')

    expect(await screen.findByRole('button', { name: 'Giriş yap' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/login')
    expect(tokenStore.get()).toBeNull()
  })

  it('shows the login page without a session and returns to the requested page after login', async () => {
    const backend = createBackend({ sessionValid: false })
    vi.stubGlobal('fetch', backend.fetchMock)
    const user = userEvent.setup()
    const { router } = renderRoutes(routes, '/secret')

    await user.type(await screen.findByLabelText('E-posta'), 'ayse@example.com')
    await user.type(screen.getByLabelText('Şifre'), 'Yanlis123')
    await user.click(screen.getByRole('button', { name: 'Giriş yap' }))
    expect(await screen.findByText('E-posta veya şifre hatalı.')).toBeInTheDocument()

    await user.clear(screen.getByLabelText('Şifre'))
    await user.type(screen.getByLabelText('Şifre'), 'Sifre1234')
    await user.click(screen.getByRole('button', { name: 'Giriş yap' }))

    expect(await screen.findByRole('heading', { name: 'Gizli sayfa' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/secret')
  })
})
