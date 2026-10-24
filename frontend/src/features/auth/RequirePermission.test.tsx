import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { Permissions } from '@/features/auth/permissions'
import { RequirePermission } from '@/features/auth/RequirePermission'
import { FakeAuth, fakeUser, renderRoutes } from '@/test/utils'

function renderUsersRoute(permissions: string[]) {
  return renderRoutes(
    [
      {
        element: (
          <FakeAuth user={fakeUser({ permissions })}>
            <RequirePermission permission={Permissions.CanManageUsers} />
          </FakeAuth>
        ),
        children: [{ path: '/users', element: <p>Kullanıcı listesi</p> }],
      },
    ],
    '/users',
  )
}

describe('RequirePermission', () => {
  it('shows the 403 screen without the policy', () => {
    renderUsersRoute([])

    expect(screen.getByText('Erişim yetkiniz yok')).toBeInTheDocument()
    expect(screen.queryByText('Kullanıcı listesi')).not.toBeInTheDocument()
  })

  it('renders the page with the policy', () => {
    renderUsersRoute([Permissions.CanManageUsers])

    expect(screen.getByText('Kullanıcı listesi')).toBeInTheDocument()
  })
})
