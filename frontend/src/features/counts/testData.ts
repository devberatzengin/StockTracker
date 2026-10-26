import type { CountDetail, CountLine, CountReview, CountReviewLine } from '@/api/types'

export const screwLine: CountLine = {
  id: 'line-screw',
  lineType: 'Expected',
  locationId: 'b2',
  locationName: 'Raf B2',
  locationPath: 'Bina › Raf B2',
  productId: 'screw',
  productCode: 'PRD-SCREW1',
  productName: 'Vida M4',
  unit: 'Piece',
  itemId: undefined,
  itemCode: undefined,
  serialNumber: undefined,
  expectedQuantity: 200,
  countedQuantity: undefined,
  resolution: undefined,
}

export const laptopLine: CountLine = {
  ...screwLine,
  id: 'line-laptop',
  lineType: 'Unexpected',
  productId: 'laptop',
  productCode: 'PRD-LAPTOP',
  productName: 'Laptop',
  itemId: 'item-1',
  itemCode: 'ITM-AAAAAA',
  expectedQuantity: 0,
  countedQuantity: 1,
}

export const countDetail = (overrides: Partial<CountDetail> = {}): CountDetail => ({
  id: 'count-1',
  locationId: 'bina',
  locationName: 'Bina',
  locationPath: 'Bina',
  includeChildren: true,
  status: 'Counting',
  note: undefined,
  startedBy: 'u1',
  startedByName: 'Mehmet Depocu',
  startedAt: '2026-10-26T09:00:00Z',
  approvedByName: undefined,
  approvedAt: undefined,
  approvalReferenceId: undefined,
  progress: { expectedLines: 1, countedExpectedLines: 0, unexpectedLines: 0 },
  lines: [screwLine],
  recentScans: [],
  ...overrides,
})

const reviewLine = (line: CountLine, overrides: Partial<CountReviewLine>): CountReviewLine => ({
  line,
  state: 'Matched',
  currentQuantity: 0,
  difference: 0,
  options: [],
  itemStatus: undefined,
  itemLocationPath: undefined,
  ...overrides,
})

export const countReview = (movementsSinceStart = 0): CountReview => ({
  countId: 'count-1',
  status: 'Reviewing',
  movementsSinceStart,
  lines: [
    reviewLine(
      { ...screwLine, countedQuantity: 185 },
      {
        state: 'QuantityDifference',
        currentQuantity: 200,
        difference: -15,
        options: ['ApplyDifference', 'Ignore'],
      },
    ),
    reviewLine(laptopLine, {
      state: 'UnexpectedMovable',
      currentQuantity: 0,
      difference: 1,
      options: ['MoveHere', 'Ignore'],
      itemStatus: 'InStock',
      itemLocationPath: 'Bina › Raf A1',
    }),
  ],
})
