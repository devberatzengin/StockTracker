import type { ItemDetail } from '@/api/types'
import { json } from '@/test/utils'

export const itemDetail = (overrides: Partial<ItemDetail> = {}): ItemDetail => ({
  id: 'item-1',
  code: 'ITM-AAAAAA',
  productId: 'laptop',
  productCode: 'PRD-LAPTOP',
  productName: 'Laptop',
  productIsAssignable: true,
  serialNumber: 'SN-1',
  status: 'InStock',
  currentLocationId: 'a1',
  locationPath: 'Bina › Raf A1',
  createdAt: '2026-10-25T09:00:00Z',
  availableActions: ['Transfer', 'Assign', 'SendToRepair', 'Dispose'],
  openAssignment: undefined,
  ...overrides,
})

export const assignedItem = () =>
  itemDetail({
    status: 'Assigned',
    currentLocationId: null,
    locationPath: null,
    availableActions: ['Return'],
    openAssignment: {
      id: 'asg-1',
      assigneeUserId: 'u-ali',
      assigneeName: 'Ali Saha',
      assignedAt: '2026-10-20T09:00:00Z',
      dueDate: '2026-10-27',
      isOverdue: false,
    },
  })

export const lookupHandler = () =>
  json(200, [
    { id: 'u-ali', fullName: 'Ali Saha', department: 'Saha', canLogin: false },
    { id: 'u-ayse', fullName: 'Ayşe Yılmaz', department: 'Muhasebe', canLogin: true },
  ])
