import { json } from '@/test/utils'

export const laptop = {
  id: 'laptop',
  code: 'PRD-LAPTOP',
  name: 'Laptop',
  trackingType: 'Serialized',
  unit: 'Piece',
}
export const screw = {
  id: 'screw',
  code: 'PRD-SCREW1',
  name: 'Vida M4',
  trackingType: 'Quantity',
  unit: 'Piece',
}

const productDetail = (p: typeof laptop) => ({
  ...p,
  description: null,
  categoryId: 'c1',
  categoryName: 'Genel',
  categoryIsActive: true,
  isAssignable: p.trackingType === 'Serialized',
  minStockLevel: null,
  isActive: true,
  trackingTypeLocked: false,
  barcodes: [],
  photos: [],
  createdAt: '2026-10-25T10:00:00Z',
  updatedAt: null,
})

export const tree = [
  {
    id: 'bina',
    code: 'LOC-BINA00',
    name: 'Bina',
    type: 'Building',
    isActive: true,
    children: [
      { id: 'a1', code: 'LOC-A10000', name: 'Raf A1', type: 'Shelf', isActive: true, children: [] },
      { id: 'b2', code: 'LOC-B20000', name: 'Raf B2', type: 'Shelf', isActive: true, children: [] },
    ],
  },
]

export const productHandlers = {
  'GET /api/products': () =>
    json(200, {
      items: [laptop, screw].map((p) => ({
        ...p,
        categoryId: 'c1',
        categoryName: 'Genel',
        isAssignable: false,
        minStockLevel: null,
        isActive: true,
        barcodes: [],
      })),
      totalCount: 2,
      page: 1,
      pageSize: 20,
    }),
  'GET /api/products/laptop': () => json(200, productDetail(laptop)),
  'GET /api/products/screw': () => json(200, productDetail(screw)),
  'GET /api/locations/tree': () => json(200, tree),
  'GET /api/products/screw/stock': () =>
    json(200, {
      productId: 'screw',
      trackingType: 'Quantity',
      totalQuantity: 500,
      locations: [{ locationId: 'a1', locationPath: 'Bina › Raf A1', quantity: 500 }],
      statusCounts: [],
    }),
}

export const scanResult = (
  type: string,
  id: string,
  code: string,
  title: string,
  subtitle: string | null = null,
) => json(200, { type, id, code, title, subtitle, isActive: true, status: null })
