import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { Permissions } from '@/features/auth/permissions'
import { LocationsPage } from '@/features/locations/LocationsPage'
import { sampleTree } from '@/features/locations/testTree'
import { FakeAuth, fakeUser, json, mockApi, renderRoutes } from '@/test/utils'

describe('LocationsPage', () => {
  const moves: unknown[] = []

  beforeEach(() => {
    moves.length = 0
    vi.stubGlobal(
      'fetch',
      mockApi({
        'GET /api/locations/tree': () => json(200, sampleTree),
        'POST /api/locations/oda/move': async (request) => {
          moves.push(await request.json())
          return new Response(null, { status: 204 })
        },
      }),
    )
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  function renderPage() {
    return renderRoutes(
      [
        {
          path: '/locations',
          element: (
            <FakeAuth
              user={fakeUser({
                role: 'WarehouseManager',
                permissions: [Permissions.CanManageCatalog],
              })}
            >
              <LocationsPage />
            </FakeAuth>
          ),
        },
      ],
      '/locations',
    )
  }

  it('filters the tree by search, keeping ancestors of matches', async () => {
    const user = userEvent.setup()
    renderPage()
    const tree = await screen.findByRole('tree', { name: 'Lokasyon ağacı' })
    expect(within(tree).getByText('Ek Bina')).toBeInTheDocument()

    await user.type(screen.getByPlaceholderText('Ad veya kod ara'), 'Raf B2')

    expect(within(tree).getByText('Raf B2')).toBeInTheDocument()
    expect(within(tree).getByText('Dolap 3')).toBeInTheDocument()
    expect(within(tree).queryByText('Raf A1')).not.toBeInTheDocument()
    expect(within(tree).queryByText('Ek Bina')).not.toBeInTheDocument()
  })

  it('moves the selected location to an allowed target', async () => {
    const user = userEvent.setup()
    renderPage()

    await user.click(await screen.findByText('Oda 101'))
    expect(screen.getByText('Bina › Oda 101')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Taşı' }))
    const dialog = await screen.findByRole('dialog', { name: 'Lokasyonu taşı' })
    await user.click(within(dialog).getByRole('combobox', { name: 'Hedef üst lokasyon' }))

    // Its own subtree cannot be chosen.
    expect(
      within(dialog).getByRole('option', { name: 'Bina › Oda 101 › Dolap 3', hidden: true }),
    ).toHaveAttribute('aria-disabled', 'true')

    await user.click(screen.getByRole('option', { name: 'Ek Bina › Kat 2' }))
    await user.click(within(dialog).getByRole('button', { name: 'Taşı' }))

    await vi.waitFor(() => expect(moves).toEqual([{ newParentId: 'kat2' }]))
  })
})
