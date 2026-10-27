import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { Permissions } from '@/features/auth/permissions'
import { StockOutPage } from '@/features/movements/StockOutPage'
import { productHandlers } from '@/features/movements/testData'
import { FakeAuth, fakeUser, mockApi, renderRoutes } from '@/test/utils'

const manager = fakeUser({ role: 'WarehouseManager', permissions: [Permissions.CanMoveStock] })

describe('StockOutPage', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('requires a note when the reason is "Diğer" and at least one line', async () => {
    vi.stubGlobal('fetch', mockApi(productHandlers))
    const user = userEvent.setup()
    renderRoutes(
      [
        {
          path: '/',
          element: (
            <FakeAuth user={manager}>
              <StockOutPage />
            </FakeAuth>
          ),
        },
      ],
      '/',
    )

    await user.click(await screen.findByRole('combobox', { name: /Sebep/ }))
    await user.click(await screen.findByRole('option', { name: 'Diğer' }))
    await user.click(screen.getByRole('button', { name: 'Özeti gör' }))

    expect(
      (await screen.findAllByText('Sebep "Diğer" ise not zorunludur.')).length,
    ).toBeGreaterThan(0)
    expect(screen.getByText('En az bir satır ekleyin.')).toBeInTheDocument()
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })
})
