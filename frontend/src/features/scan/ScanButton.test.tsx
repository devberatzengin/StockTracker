import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { Permissions } from '@/features/auth/permissions'
import { ScanButton } from '@/features/scan/ScanButton'
import { ScanRedirectPage } from '@/features/scan/ScanRedirectPage'
import { FakeAuth, fakeUser, json, mockApi, renderRoutes } from '@/test/utils'

const manager = fakeUser({ role: 'WarehouseManager', permissions: [Permissions.CanManageCatalog] })

const product = {
  type: 'product',
  id: 'p1',
  code: 'PRD-ABCDEF',
  title: 'Laptop',
  subtitle: 'Elektronik',
  isActive: true,
  status: null,
}

const notFoundEan = () =>
  new Response(
    JSON.stringify({
      status: 404,
      title: 'Kayıt bulunamadı.',
      suggestCreateProduct: true,
      barcode: '4006381333931',
      barcodeFormat: 'Ean13',
    }),
    { status: 404, headers: { 'Content-Type': 'application/problem+json' } },
  )

function renderButton() {
  return renderRoutes(
    [
      {
        path: '/',
        element: (
          <FakeAuth user={manager}>
            <ScanButton />
          </FakeAuth>
        ),
      },
      { path: '/products/:id', element: <p>Ürün detayı</p> },
      { path: '/products', element: <p>Ürün listesi</p> },
    ],
    '/',
  )
}

describe('ScanButton', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('opens the detail page of the scanned product', async () => {
    vi.stubGlobal('fetch', mockApi({ 'GET /api/scan/PRD-ABCDEF': () => json(200, product) }))
    const user = userEvent.setup()
    const { router } = renderButton()

    await user.click(screen.getByRole('button', { name: 'Tara' }))
    await user.type(await screen.findByLabelText('Elle giriş'), 'PRD-ABCDEF{Enter}')

    await waitFor(() => expect(router.state.location.pathname).toBe('/products/p1'))
  })

  it('offers to create a product for an unknown EAN', async () => {
    vi.stubGlobal('fetch', mockApi({ 'GET /api/scan/4006381333931': notFoundEan }))
    const user = userEvent.setup()
    const { router } = renderButton()

    await user.click(screen.getByRole('button', { name: 'Tara' }))
    await user.type(await screen.findByLabelText('Elle giriş'), '4006381333931{Enter}')

    expect(await screen.findByText('Kayıt bulunamadı')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Bu barkodla yeni ürün oluştur' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/products'))
    expect(router.state.location.search).toBe('?newBarcode=4006381333931&barcodeFormat=Ean13')
  })
})

describe('ScanRedirectPage', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('resolves a label URL code and redirects to the detail page', async () => {
    vi.stubGlobal(
      'fetch',
      mockApi({
        'GET /api/scan/LOC-123456': () =>
          json(200, {
            ...product,
            type: 'location',
            id: 'l1',
            code: 'LOC-123456',
            title: 'Raf A1',
          }),
      }),
    )
    const { router } = renderRoutes(
      [
        {
          path: '/s/:code',
          element: (
            <FakeAuth user={manager}>
              <ScanRedirectPage />
            </FakeAuth>
          ),
        },
        { path: '/locations/:id', element: <p>Lokasyon detayı</p> },
      ],
      '/s/LOC-123456',
    )

    expect(await screen.findByText('Lokasyon detayı')).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/locations/l1')
  })
})
