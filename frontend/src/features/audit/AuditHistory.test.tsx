import { screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { AuditHistory } from '@/features/audit/AuditHistory'
import { formatAuditValue } from '@/features/audit/auditLabels'
import { json, mockApi, renderRoutes } from '@/test/utils'

const row = (overrides: Record<string, unknown>) => ({
  id: crypto.randomUUID(),
  entityType: 'Product',
  entityId: 'p1',
  entityLabel: 'Laptop Pro',
  parentEntityId: undefined,
  action: 'Updated',
  changes: [],
  userId: 'u1',
  userName: 'Ayşe Yılmaz',
  createdAt: '2026-10-27T09:00:00Z',
  ...overrides,
})

describe('AuditHistory', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('shows old → new values in Turkish and hides technical fields', async () => {
    vi.stubGlobal(
      'fetch',
      mockApi({
        'GET /api/audit': () =>
          json(200, {
            items: [
              row({
                changes: [
                  { field: 'name', old: 'Laptop', new: 'Laptop Pro' },
                  { field: 'trackingType', old: 'Quantity', new: 'Serialized' },
                  { field: 'path', old: '/a/', new: '/b/' },
                ],
              }),
              row({
                entityType: 'ProductBarcode',
                entityLabel: undefined,
                action: 'Deleted',
                changes: [{ field: 'value', old: '4006381333931', new: undefined }],
              }),
            ],
            totalCount: 2,
            page: 1,
            pageSize: 20,
          }),
      }),
    )
    renderRoutes(
      [
        {
          path: '/',
          element: <AuditHistory filters={{ entityType: 'Product', entityId: 'p1' }} />,
        },
      ],
      '/',
    )

    expect(await screen.findByText('Laptop Pro')).toBeInTheDocument()
    expect(screen.getByText('Ad')).toBeInTheDocument()
    expect(screen.getByText('Laptop')).toBeInTheDocument()
    expect(screen.getByText('Tekil (seri numaralı)')).toBeInTheDocument()
    expect(screen.queryByText('/a/')).not.toBeInTheDocument()
    // Child rows say what they are about.
    expect(screen.getAllByText('Barkod')).toHaveLength(2) // record type and field name
    expect(screen.getByText('Silindi')).toBeInTheDocument()
    expect(screen.getAllByText(/Ayşe Yılmaz/)).toHaveLength(2)
  })

  it('formats values', () => {
    expect(formatAuditValue('isActive', 'false')).toBe('Hayır')
    expect(formatAuditValue('role', 'WarehouseManager')).toBe('Depo Sorumlusu')
    expect(formatAuditValue('categoryId', '01a1787e-5e8f-7210-9226-983404fac1fd')).toBe('#01a1787e')
    expect(formatAuditValue('description', null)).toBe('—')
  })
})
