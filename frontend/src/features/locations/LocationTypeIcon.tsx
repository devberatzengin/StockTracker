import {
  IconBox,
  IconBuilding,
  IconDoor,
  IconLayoutRows,
  IconMapPin,
  IconStack2,
  type IconProps,
} from '@tabler/icons-react'
import type { ComponentType } from 'react'
import type { LocationType } from '@/api/types'

const ICONS: Record<LocationType, ComponentType<IconProps>> = {
  Building: IconBuilding,
  Room: IconDoor,
  Cabinet: IconLayoutRows,
  Shelf: IconStack2,
  Box: IconBox,
  Other: IconMapPin,
}

export function LocationTypeIcon({ type, size = 16 }: { type: LocationType; size?: number }) {
  const Icon = ICONS[type]
  return <Icon size={size} stroke={1.6} />
}
