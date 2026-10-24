import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { UserFormModal } from '@/features/users/UserFormModal'
import { json, mockApi, renderRoutes } from '@/test/utils'

function renderModal() {
  return renderRoutes([{ path: '/', element: <UserFormModal opened onClose={() => {}} /> }], '/')
}

describe('UserFormModal', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('hides email and password when login is disabled', async () => {
    const user = userEvent.setup()
    renderModal()

    expect(await screen.findByLabelText(/E-posta/)).toBeInTheDocument()
    expect(screen.getByLabelText(/^Şifre/)).toBeInTheDocument()

    await user.click(screen.getByRole('switch', { name: /Giriş yetkisi/ }))

    expect(screen.queryByLabelText(/E-posta/)).not.toBeInTheDocument()
    expect(screen.queryByLabelText(/^Şifre/)).not.toBeInTheDocument()
  })

  it('creates a user without login using only the name', async () => {
    const requests: unknown[] = []
    vi.stubGlobal(
      'fetch',
      mockApi({
        'POST /api/users': async (request) => {
          requests.push(await request.json())
          return json(201, { id: 'new-id' })
        },
      }),
    )
    const user = userEvent.setup()
    renderModal()

    await user.type(await screen.findByLabelText(/Ad soyad/), 'Saha Personeli')
    await user.click(screen.getByRole('switch', { name: /Giriş yetkisi/ }))
    await user.click(screen.getByRole('button', { name: 'Kaydet' }))

    await vi.waitFor(() => expect(requests).toHaveLength(1))
    expect(requests[0]).toEqual({
      fullName: 'Saha Personeli',
      department: null,
      role: 'Staff',
      canLogin: false,
      email: null,
      password: null,
    })
  })

  it('shows backend field errors on the form', async () => {
    vi.stubGlobal(
      'fetch',
      mockApi({
        'POST /api/users': () =>
          new Response(
            JSON.stringify({
              status: 400,
              errors: { Email: ['Bu e-posta adresi zaten kullanılıyor.'] },
            }),
            { status: 400, headers: { 'Content-Type': 'application/problem+json' } },
          ),
      }),
    )
    const user = userEvent.setup()
    renderModal()

    await user.type(await screen.findByLabelText(/Ad soyad/), 'Ali Veli')
    await user.type(screen.getByLabelText(/E-posta/), 'ali@example.com')
    await user.type(screen.getByLabelText(/^Şifre/), 'Sifre1234')
    await user.click(screen.getByRole('button', { name: 'Kaydet' }))

    expect(await screen.findByText('Bu e-posta adresi zaten kullanılıyor.')).toBeInTheDocument()
  })
})
