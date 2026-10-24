import {
  Alert,
  Badge,
  Button,
  Group,
  SimpleGrid,
  Stack,
  Table,
  Tabs,
  Text,
  Title,
} from '@mantine/core'
import { useDisclosure } from '@mantine/hooks'
import { modals } from '@mantine/modals'
import { notifications } from '@mantine/notifications'
import { IconArchive, IconArrowLeft, IconPencil } from '@tabler/icons-react'
import type { ReactNode } from 'react'
import { Link, useParams } from 'react-router'
import { isApiError } from '@/api/client'
import type { ProductDto } from '@/api/types'
import { NotFoundPage } from '@/components/NotFoundPage'
import { usePermission } from '@/features/auth/authContext'
import { Permissions } from '@/features/auth/permissions'
import { useDeactivateProduct, useProduct } from '@/features/catalog/catalogApi'
import {
  BARCODE_FORMAT_LABELS,
  formatQuantity,
  TRACKING_TYPE_LABELS,
  UNIT_LABELS,
} from '@/features/catalog/labels'
import { ProductFormModal } from '@/features/catalog/ProductFormModal'

const dateFormatter = new Intl.DateTimeFormat('tr-TR', {
  timeZone: 'Europe/Istanbul',
  dateStyle: 'medium',
  timeStyle: 'short',
})

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div>
      <Text size="xs" c="dimmed" fw={500}>
        {label}
      </Text>
      <Text size="sm" component="div">
        {children}
      </Text>
    </div>
  )
}

export function ProductDetailPage() {
  const { id = '' } = useParams()
  const { data: product, isPending, error } = useProduct(id)

  if (isApiError(error, 404) || isApiError(error, 400)) {
    return <NotFoundPage />
  }

  if (isPending || !product) {
    return <Text c="dimmed">Yükleniyor…</Text>
  }

  return <ProductDetail product={product} />
}

function ProductDetail({ product }: { product: ProductDto }) {
  const canManage = usePermission(Permissions.CanManageCatalog)
  const deactivate = useDeactivateProduct()
  const [formOpened, form] = useDisclosure()

  const confirmDeactivate = () =>
    modals.openConfirmModal({
      title: 'Ürünü pasifleştir',
      children: (
        <Text size="sm">
          <b>{product.name}</b> pasifleştirilsin mi? Pasif ürün listelerde gizlenir ve yeni harekete
          giremez.
        </Text>
      ),
      labels: { confirm: 'Pasifleştir', cancel: 'Vazgeç' },
      confirmProps: { color: 'red' },
      onConfirm: () =>
        deactivate.mutate(product.id, {
          onSuccess: () => notifications.show({ color: 'green', message: 'Ürün pasifleştirildi.' }),
        }),
    })

  return (
    <Stack>
      <Button
        component={Link}
        to="/products"
        variant="subtle"
        leftSection={<IconArrowLeft size={16} />}
        w="fit-content"
        px={0}
      >
        Ürünler
      </Button>

      <Group justify="space-between" align="flex-start">
        <div>
          <Group gap="xs">
            <Title order={2}>{product.name}</Title>
            {!product.isActive && <Badge color="gray">Pasif</Badge>}
          </Group>
          <Text ff="monospace" c="dimmed">
            {product.code}
          </Text>
        </div>
        {canManage && (
          <Group>
            <Button variant="light" leftSection={<IconPencil size={16} />} onClick={form.open}>
              Düzenle
            </Button>
            {product.isActive && (
              <Button
                variant="light"
                color="red"
                leftSection={<IconArchive size={16} />}
                onClick={confirmDeactivate}
              >
                Pasifleştir
              </Button>
            )}
          </Group>
        )}
      </Group>

      {!product.isActive && (
        <Alert color="gray">
          Bu ürün pasif. Listelerde varsayılan olarak gizlenir ve yeni harekete giremez.
        </Alert>
      )}

      <Tabs defaultValue="general">
        <Tabs.List>
          <Tabs.Tab value="general">Genel</Tabs.Tab>
        </Tabs.List>

        <Tabs.Panel value="general" pt="md">
          <Stack>
            <SimpleGrid cols={{ base: 1, xs: 2, md: 3 }}>
              <Field label="Kategori">
                {product.categoryName}
                {!product.categoryIsActive && ' (pasif)'}
              </Field>
              <Field label="Takip tipi">{TRACKING_TYPE_LABELS[product.trackingType]}</Field>
              <Field label="Birim">{UNIT_LABELS[product.unit]}</Field>
              <Field label="Zimmetlenebilir">{product.isAssignable ? 'Evet' : 'Hayır'}</Field>
              <Field label="Minimum stok">{formatQuantity(product.minStockLevel)}</Field>
              <Field label="Oluşturulma">{dateFormatter.format(new Date(product.createdAt))}</Field>
            </SimpleGrid>

            <Field label="Açıklama">{product.description ?? '—'}</Field>

            <div>
              <Text size="xs" c="dimmed" fw={500} mb={4}>
                Barkodlar
              </Text>
              {product.barcodes.length === 0 ? (
                <Text size="sm">—</Text>
              ) : (
                <Table withTableBorder maw={420}>
                  <Table.Tbody>
                    {product.barcodes.map((barcode) => (
                      <Table.Tr key={barcode.id}>
                        <Table.Td ff="monospace">{barcode.value}</Table.Td>
                        <Table.Td w={110}>
                          <Badge variant="light">{BARCODE_FORMAT_LABELS[barcode.format]}</Badge>
                        </Table.Td>
                      </Table.Tr>
                    ))}
                  </Table.Tbody>
                </Table>
              )}
            </div>
          </Stack>
        </Tabs.Panel>
      </Tabs>

      {canManage && <ProductFormModal opened={formOpened} onClose={form.close} product={product} />}
    </Stack>
  )
}
