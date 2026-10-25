import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { Permissions } from '@/features/auth/permissions'
import { StockInPage } from '@/features/movements/StockInPage'
import { productHandlers } from '@/features/movements/testData'
import { FakeAuth, fakeUser, json, mockApi, renderRoutes } from '@/test/utils'

const manager = fakeUser({ role: 'WarehouseManager', permissions: [Permissions.CanMoveStock] })

function renderPage() {
  return renderRoutes(
    [
      {
        path: '/movements/in',
        element: (
          <FakeAuth user={manager}>
            <StockInPage />
          </FakeAuth>
        ),
      },
      { path: '/movements/ref/:referenceId', element: <p>Özet sayfası</p> },
    ],
    '/movements/in',
  )
}

async function chooseOption(
  user: ReturnType<typeof userEvent.setup>,
  combobox: HTMLElement,
  option: string,
) {
  await user.click(combobox)
  await user.click(await screen.findByRole('option', { name: option }))
}

describe('StockInPage', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('sends serialized and quantity lines and opens the summary page', async () => {
    const requests: unknown[] = []
    vi.stubGlobal(
      'fetch',
      mockApi({
        ...productHandlers,
        'POST /api/movements/in': async (request) => {
          requests.push(await request.json())
          return json(200, { referenceId: 'ref-1', movementCount: 3, createdItems: [] })
        },
      }),
    )
    const user = userEvent.setup()
    const { router } = renderPage()

    await chooseOption(
      user,
      await screen.findByRole('combobox', { name: /Hedef lokasyon/ }),
      'Bina › Raf A1',
    )

    await chooseOption(user, screen.getByRole('combobox', { name: 'Ürün' }), 'Laptop · PRD-LAPTOP')
    await user.type(screen.getByLabelText('Adet'), '2')
    await user.type(
      screen.getAllByLabelText(/Seri numaraları/).find((el) => el.tagName === 'INPUT')!,
      'SN-1{Enter}',
    )

    await user.click(screen.getByRole('button', { name: 'Satır ekle' }))
    await chooseOption(
      user,
      screen.getAllByRole('combobox', { name: 'Ürün' })[1],
      'Vida M4 · PRD-SCREW1',
    )
    await user.type(screen.getByLabelText(/Miktar/), '500')

    await user.click(screen.getByRole('button', { name: 'Özeti gör' }))
    await user.click(await screen.findByRole('button', { name: 'Onayla' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/movements/ref/ref-1'))
    expect(requests[0]).toEqual({
      toLocationId: 'a1',
      lines: [
        { productId: 'laptop', quantity: 2, serialNumbers: ['SN-1'] },
        { productId: 'screw', quantity: 500, serialNumbers: null },
      ],
    })
  })

  it('shows a backend line error on its row', async () => {
    vi.stubGlobal(
      'fetch',
      mockApi({
        ...productHandlers,
        'POST /api/movements/in': () =>
          new Response(
            JSON.stringify({
              status: 422,
              code: 'Item.SerialNumberTaken',
              detail: 'x',
              errors: {
                'lines[0].serialNumbers': ['"SN-1" seri numarası bu üründe zaten kayıtlı.'],
              },
            }),
            { status: 422, headers: { 'Content-Type': 'application/problem+json' } },
          ),
      }),
    )
    const user = userEvent.setup()
    renderPage()

    await chooseOption(
      user,
      await screen.findByRole('combobox', { name: /Hedef lokasyon/ }),
      'Bina › Raf A1',
    )
    await chooseOption(user, screen.getByRole('combobox', { name: 'Ürün' }), 'Laptop · PRD-LAPTOP')
    await user.type(screen.getByLabelText('Adet'), '1')
    await user.type(
      screen.getAllByLabelText(/Seri numaraları/).find((el) => el.tagName === 'INPUT')!,
      'SN-1{Enter}',
    )
    await user.click(screen.getByRole('button', { name: 'Özeti gör' }))
    await user.click(await screen.findByRole('button', { name: 'Onayla' }))

    expect(
      await screen.findByText('"SN-1" seri numarası bu üründe zaten kayıtlı.'),
    ).toBeInTheDocument()
    expect(screen.getByText(/Hiçbir hareket kaydedilmedi/)).toBeInTheDocument()
  })

  it('validates lines before submitting', async () => {
    vi.stubGlobal('fetch', mockApi(productHandlers))
    const user = userEvent.setup()
    renderPage()

    await user.click(await screen.findByRole('button', { name: 'Özeti gör' }))

    expect(screen.getByText('Hedef lokasyon seçin.')).toBeInTheDocument()
    expect(screen.getByText('Ürün seçin.')).toBeInTheDocument()
  })
})
