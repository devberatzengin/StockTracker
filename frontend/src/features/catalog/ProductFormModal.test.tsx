import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { ProductDto } from '@/api/types'
import { ProductFormModal } from '@/features/catalog/ProductFormModal'
import { json, mockApi, renderRoutes } from '@/test/utils'

const CATEGORY_ID = '11111111-1111-1111-1111-111111111111'

const categories = json(200, {
  items: [{ id: CATEGORY_ID, name: 'Elektronik', isActive: true, activeProductCount: 0 }],
  totalCount: 1,
  page: 1,
  pageSize: 100,
})

function renderForm(product?: ProductDto) {
  return renderRoutes(
    [
      { path: '/', element: <ProductFormModal opened onClose={() => {}} product={product} /> },
      { path: '/products/:id', element: <p>Ürün detayı</p> },
    ],
    '/',
  )
}

const radio = (name: string) => screen.getByRole('radio', { name })
const assignable = () => screen.getByRole('checkbox', { name: /Zimmetlenebilir/ })
const unitSelect = () => screen.getByRole('combobox', { name: /^Birim/ })

describe('ProductFormModal', () => {
  const requests: { method: string; path: string; body: unknown }[] = []
  let categoriesLoaded = false

  beforeEach(() => {
    requests.length = 0
    categoriesLoaded = false
    vi.stubGlobal(
      'fetch',
      mockApi({
        'GET /api/categories': () => {
          categoriesLoaded = true
          return categories.clone()
        },
        'POST /api/products': async (request) => {
          requests.push({ method: 'POST', path: '/api/products', body: await request.json() })
          return json(201, { id: 'new-product' })
        },
      }),
    )
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('only enables "assignable" for serialized products', async () => {
    const user = userEvent.setup()
    renderForm()

    expect(await screen.findByRole('radio', { name: 'Miktar bazlı' })).toBeChecked()
    expect(assignable()).toBeDisabled()

    await user.click(radio('Tekil (seri numaralı)'))
    expect(assignable()).toBeEnabled()
    await user.click(assignable())
    expect(assignable()).toBeChecked()

    // Switching back clears and disables it.
    await user.click(radio('Miktar bazlı'))
    expect(assignable()).toBeDisabled()
    expect(assignable()).not.toBeChecked()
  })

  it('locks the unit to piece for serialized products', async () => {
    const user = userEvent.setup()
    renderForm()

    await user.click(await screen.findByRole('radio', { name: 'Tekil (seri numaralı)' }))

    expect(unitSelect()).toBeDisabled()
    expect(unitSelect()).toHaveValue('Adet')
  })

  it('creates a serialized, assignable product with its barcodes', async () => {
    const user = userEvent.setup()
    const { router } = renderForm()

    await user.type(await screen.findByLabelText(/Ürün adı/), 'Laptop')
    await waitFor(() => expect(categoriesLoaded).toBe(true))
    await user.click(screen.getByRole('combobox', { name: /^Kategori/ }))
    await user.click(await screen.findByRole('option', { name: 'Elektronik' }))
    await user.click(radio('Tekil (seri numaralı)'))
    await user.click(assignable())

    await user.type(screen.getByLabelText('Barkod değeri'), '4006381 333931{Enter}')
    await user.type(screen.getByLabelText('Barkod değeri'), 'LAPTOP-01')
    await user.click(screen.getByRole('combobox', { name: 'Barkod formatı' }))
    await user.click(await screen.findByRole('option', { name: 'Code 128' }))
    await user.click(screen.getByRole('button', { name: 'Ekle' }))
    expect(screen.getByText('4006381333931')).toBeInTheDocument()

    // Removing a pending barcode drops it from the request.
    await user.click(screen.getByRole('button', { name: 'LAPTOP-01 barkodunu sil' }))

    await user.click(screen.getByRole('button', { name: 'Kaydet' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/products/new-product'))
    expect(requests).toHaveLength(1)
    expect(requests[0].body).toEqual({
      name: 'Laptop',
      description: null,
      categoryId: CATEGORY_ID,
      unit: 'Piece',
      trackingType: 'Serialized',
      isAssignable: true,
      minStockLevel: null,
      barcodes: [{ value: '4006381333931', format: 'Ean13' }],
    })
  })

  it('rejects the same barcode twice in the list', async () => {
    const user = userEvent.setup()
    renderForm()

    await user.type(await screen.findByLabelText('Barkod değeri'), '4006381333931{Enter}')
    await user.type(screen.getByLabelText('Barkod değeri'), '4006381333931{Enter}')

    expect(screen.getByText('Bu barkod listede zaten var.')).toBeInTheDocument()
  })

  it('locks the tracking type when the product has stock history', async () => {
    renderForm({
      id: 'p1',
      code: 'PRD-ABC123',
      name: 'Vida M4',
      description: null,
      categoryId: CATEGORY_ID,
      categoryName: 'Elektronik',
      categoryIsActive: true,
      unit: 'Piece',
      trackingType: 'Quantity',
      isAssignable: false,
      minStockLevel: 100,
      isActive: true,
      trackingTypeLocked: true,
      barcodes: [],
      createdAt: '2026-10-24T10:00:00Z',
      updatedAt: null,
    })

    expect(await screen.findByRole('radio', { name: 'Tekil (seri numaralı)' })).toBeDisabled()
    expect(screen.getByText(/takip tipi değiştirilemez/)).toBeInTheDocument()
    expect(screen.getByText('Barkod ekleme ve silme anında kaydedilir.')).toBeInTheDocument()
  })
})
