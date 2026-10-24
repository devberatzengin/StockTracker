import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import type { CurrentUser } from '@/api/types'
import { AppLayout } from '@/components/layout/AppLayout'
import { Permissions } from '@/features/auth/permissions'
import { FakeAuth, fakeUser, renderRoutes } from '@/test/utils'

function renderLayout(user: CurrentUser) {
  return renderRoutes(
    [
      {
        element: (
          <FakeAuth user={user}>
            <AppLayout />
          </FakeAuth>
        ),
        children: [{ path: '/', element: <p>Sayfa</p> }],
      },
    ],
    '/',
  )
}

describe('AppLayout', () => {
  it('renders the page, the user and the role label', () => {
    renderLayout(fakeUser())

    expect(screen.getByText('Sayfa')).toBeInTheDocument()
    expect(screen.getByText('Ayşe Yılmaz')).toBeInTheDocument()
    expect(screen.getByText('Personel')).toBeInTheDocument()
  })

  it('hides the users menu item from staff', () => {
    renderLayout(fakeUser())

    expect(screen.queryByRole('link', { name: /Kullanıcılar/ })).not.toBeInTheDocument()
  })

  it('shows the users menu item to admins', () => {
    renderLayout(fakeUser({ role: 'Admin', permissions: [Permissions.CanManageUsers] }))

    expect(screen.getByRole('link', { name: /Kullanıcılar/ })).toHaveAttribute('href', '/users')
  })
})
