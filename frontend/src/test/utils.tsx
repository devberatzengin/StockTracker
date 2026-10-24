import { MantineProvider } from '@mantine/core'
import { ModalsProvider } from '@mantine/modals'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import type { ReactNode } from 'react'
import { createMemoryRouter, RouterProvider, type RouteObject } from 'react-router'
import type { CurrentUser } from '@/api/types'
import { AuthContext, type AuthContextValue } from '@/features/auth/authContext'
import type { Permission } from '@/features/auth/permissions'

export function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
}

export function Providers({
  children,
  queryClient,
}: {
  children: ReactNode
  queryClient?: QueryClient
}) {
  return (
    // env="test" disables transitions so dropdown options are immediately accessible.
    <MantineProvider env="test">
      <QueryClientProvider client={queryClient ?? createTestQueryClient()}>
        <ModalsProvider>{children}</ModalsProvider>
      </QueryClientProvider>
    </MantineProvider>
  )
}

export function renderRoutes(
  routes: RouteObject[],
  initialPath: string,
  queryClient?: QueryClient,
) {
  const router = createMemoryRouter(routes, { initialEntries: [initialPath] })
  const result = render(
    <Providers queryClient={queryClient}>
      <RouterProvider router={router} />
    </Providers>,
  )
  return { ...result, router }
}

export function fakeUser(overrides: Partial<CurrentUser> = {}): CurrentUser {
  return {
    id: '00000000-0000-0000-0000-000000000001',
    fullName: 'Ayşe Yılmaz',
    email: 'ayse@example.com',
    department: null,
    role: 'Staff',
    permissions: [],
    ...overrides,
  }
}

/** Static auth context for components that only read the session. */
export function FakeAuth({ user, children }: { user: CurrentUser | null; children: ReactNode }) {
  const value: AuthContextValue = {
    status: user ? 'authenticated' : 'anonymous',
    user,
    login: async () => {},
    logout: async () => {},
    changePassword: async () => {},
    hasPermission: (permission: Permission) => user?.permissions.includes(permission) ?? false,
  }
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

type Handler = (request: Request) => Response | Promise<Response>

/**
 * Routes fetch calls to handlers keyed by "METHOD /api/path" (query string ignored).
 * Unhandled requests fail the test loudly.
 */
export function mockApi(handlers: Record<string, Handler>) {
  return async (input: RequestInfo | URL, init?: RequestInit) => {
    const request = new Request(new URL(String(input), 'http://localhost'), init)
    const key = `${request.method} ${new URL(request.url).pathname}`
    const handler = handlers[key]
    if (!handler) {
      throw new Error(`Unhandled request: ${key}`)
    }
    return handler(request)
  }
}

export const json = (status: number, body: unknown) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })

export const empty = (status: number) => new Response(null, { status })
