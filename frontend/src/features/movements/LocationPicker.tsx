import { ActionIcon, Alert, Group, Select, Tooltip } from '@mantine/core'
import { useDisclosure } from '@mantine/hooks'
import { IconScan } from '@tabler/icons-react'
import { useMemo, useState } from 'react'
import { getErrorMessage, isApiError } from '@/api/client'
import { useLocationTree } from '@/features/locations/locationsApi'
import { flattenTree } from '@/features/locations/treeUtils'
import { lookupScan } from '@/features/scan/scanApi'
import { ScanDialog } from '@/features/scan/ScanDialog'

interface Props {
  label: string
  value: string | null
  onChange: (locationId: string | null) => void
  error?: string
  required?: boolean
  /** Locations that cannot be chosen (e.g. the source of a transfer line). */
  disabledIds?: string[]
}

/** Location select labelled with full paths ("Bina › Oda › Raf A1"); also accepts a scanned label. */
export function LocationPicker({
  label,
  value,
  onChange,
  error,
  required,
  disabledIds = [],
}: Props) {
  const { data: tree = [] } = useLocationTree()
  const [scanning, scan] = useDisclosure()
  const [scanError, setScanError] = useState<string | null>(null)

  const options = useMemo(
    () =>
      flattenTree(tree).map((f) => ({
        value: f.node.id,
        label: f.pathLabel,
        disabled: disabledIds.includes(f.node.id),
      })),
    [tree, disabledIds],
  )

  const onScan = async (input: string) => {
    setScanError(null)
    try {
      const result = await lookupScan(input)
      if (result.type !== 'location') {
        setScanError(
          `"${result.title}" bir lokasyon değil. Rafın ya da lokasyonun etiketini okutun.`,
        )
        return
      }
      onChange(result.id)
      scan.close()
    } catch (e) {
      setScanError(isApiError(e, 404) ? 'Bu koda ait lokasyon bulunamadı.' : getErrorMessage(e))
    }
  }

  return (
    <>
      <Group align="flex-end" gap="xs" wrap="nowrap">
        <Select
          label={label}
          placeholder="Lokasyon ara"
          data={options}
          value={value}
          onChange={onChange}
          searchable
          clearable
          required={required}
          error={error}
          nothingFoundMessage="Lokasyon bulunamadı"
          style={{ flex: 1 }}
        />
        <Tooltip label="Lokasyon etiketini okut">
          <ActionIcon
            size={36}
            variant="light"
            aria-label={`${label} okut`}
            onClick={scan.open}
            mb={error ? 22 : 0}
          >
            <IconScan size={18} />
          </ActionIcon>
        </Tooltip>
      </Group>
      <ScanDialog
        opened={scanning}
        onClose={() => {
          setScanError(null)
          scan.close()
        }}
        onScan={onScan}
        title="Lokasyon okut"
      >
        {scanError && <Alert color="orange">{scanError}</Alert>}
      </ScanDialog>
    </>
  )
}
