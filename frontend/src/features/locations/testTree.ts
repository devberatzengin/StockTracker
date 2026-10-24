import type { LocationTreeNode } from '@/api/types'

const node = (
  id: string,
  name: string,
  type: LocationTreeNode['type'],
  children: LocationTreeNode[] = [],
) => ({
  id,
  code: `LOC-${id.toUpperCase().padEnd(6, '0').slice(0, 6)}`,
  name,
  type,
  isActive: true,
  children,
})

/** Bina › Oda 101 › Dolap 3 › Raf A1 / Raf B2, plus a second building. */
export const sampleTree: LocationTreeNode[] = [
  node('bina', 'Bina', 'Building', [
    node('oda', 'Oda 101', 'Room', [
      node('dolap', 'Dolap 3', 'Cabinet', [
        node('rafa1', 'Raf A1', 'Shelf'),
        node('rafb2', 'Raf B2', 'Shelf'),
      ]),
    ]),
  ]),
  node('bina2', 'Ek Bina', 'Building', [node('kat2', 'Kat 2', 'Other')]),
]
