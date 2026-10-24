import { zodResolver } from '@hookform/resolvers/zod'
import {
  Alert,
  Button,
  Checkbox,
  Divider,
  Group,
  Modal,
  NumberInput,
  Radio,
  Select,
  Stack,
  Text,
  Textarea,
  TextInput,
} from '@mantine/core'
import { notifications } from '@mantine/notifications'
import { useState } from 'react'
import { Controller, useForm, useWatch } from 'react-hook-form'
import { useNavigate } from 'react-router'
import { z } from 'zod'
import { getErrorMessage } from '@/api/client'
import { applyServerErrors } from '@/api/formErrors'
import type { BarcodeFormat, ProductDto, TrackingType, Unit } from '@/api/types'
import { BarcodeEditor, type BarcodeRow } from '@/features/catalog/BarcodeEditor'
import {
  useAddBarcode,
  useCategoryOptions,
  useCreateProduct,
  useRemoveBarcode,
  useUpdateProduct,
} from '@/features/catalog/catalogApi'
import { TRACKING_TYPE_LABELS, UNIT_OPTIONS } from '@/features/catalog/labels'

const FIELDS = [
  'name',
  'description',
  'categoryId',
  'unit',
  'trackingType',
  'isAssignable',
  'minStockLevel',
] as const

const schema = z
  .object({
    name: z.string().trim().min(1, 'Ürün adı zorunludur.').max(150, 'En fazla 150 karakter.'),
    description: z.string().trim().max(1000, 'En fazla 1000 karakter.'),
    categoryId: z.string().min(1, 'Kategori seçin.'),
    trackingType: z.enum(['Serialized', 'Quantity']),
    unit: z.enum(['Piece', 'Kg', 'Meter', 'Liter', 'Package', 'Box']),
    isAssignable: z.boolean(),
    minStockLevel: z.number().min(0, 'Negatif olamaz.').nullable(),
  })
  .superRefine((v, ctx) => {
    if (
      v.trackingType === 'Serialized' &&
      v.minStockLevel !== null &&
      !Number.isInteger(v.minStockLevel)
    ) {
      ctx.addIssue({
        code: 'custom',
        path: ['minStockLevel'],
        message: 'Tekil takipte tam sayı olmalıdır.',
      })
    }
  })

type FormValues = z.infer<typeof schema>

function toFormValues(product?: ProductDto): FormValues {
  return {
    name: product?.name ?? '',
    description: product?.description ?? '',
    categoryId: product?.categoryId ?? '',
    trackingType: product?.trackingType ?? 'Quantity',
    unit: product?.unit ?? 'Piece',
    isAssignable: product?.isAssignable ?? false,
    minStockLevel: product?.minStockLevel ?? null,
  }
}

interface Props {
  opened: boolean
  onClose: () => void
  /** Present when editing. */
  product?: ProductDto
  /** Pre-filled barcode for a new product, e.g. after scanning an unknown EAN. */
  initialBarcode?: { value: string; format: BarcodeFormat } | null
}

export function ProductFormModal({ opened, onClose, product, initialBarcode }: Props) {
  return (
    <Modal
      opened={opened}
      onClose={onClose}
      size="lg"
      title={product ? 'Ürünü düzenle' : 'Yeni ürün'}
    >
      {/* Modal content unmounts on close, so every open starts with fresh form state. */}
      <ProductForm product={product} onClose={onClose} initialBarcode={initialBarcode} />
    </Modal>
  )
}

function ProductForm({
  product,
  onClose,
  initialBarcode,
}: {
  product?: ProductDto
  onClose: () => void
  initialBarcode?: { value: string; format: BarcodeFormat } | null
}) {
  const navigate = useNavigate()
  const createProduct = useCreateProduct()
  const updateProduct = useUpdateProduct()
  const { data: categories } = useCategoryOptions()
  const [formError, setFormError] = useState<string | null>(null)
  // New products send their barcodes with the create request.
  const [pendingBarcodes, setPendingBarcodes] = useState<BarcodeRow[]>(() =>
    initialBarcode ? [{ key: 'initial', ...initialBarcode }] : [],
  )

  const {
    register,
    control,
    handleSubmit,
    setValue,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: toFormValues(product) })

  const trackingType = useWatch({ control, name: 'trackingType' })
  const isSerialized = trackingType === 'Serialized'
  const trackingTypeLocked = product?.trackingTypeLocked ?? false

  const categoryOptions = (categories?.items ?? []).map((c) => ({ value: c.id, label: c.name }))
  // An inactive current category is not in the active list; keep it selectable while editing.
  if (product && !product.categoryIsActive) {
    categoryOptions.push({ value: product.categoryId, label: `${product.categoryName} (pasif)` })
  }

  const onTrackingTypeChange = (value: TrackingType) => {
    setValue('trackingType', value)
    if (value === 'Serialized') {
      setValue('unit', 'Piece')
    } else {
      setValue('isAssignable', false)
    }
  }

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null)
    const body = {
      name: values.name,
      description: values.description || null,
      categoryId: values.categoryId,
      unit: values.unit,
      trackingType: values.trackingType,
      isAssignable: values.isAssignable,
      minStockLevel: values.minStockLevel,
    }

    try {
      if (product) {
        await updateProduct.mutateAsync({ id: product.id, body })
        notifications.show({ color: 'green', message: 'Ürün güncellendi.' })
        onClose()
      } else {
        const { id } = await createProduct.mutateAsync({
          ...body,
          barcodes: pendingBarcodes.map(({ value, format }) => ({ value, format })),
        })
        notifications.show({ color: 'green', message: 'Ürün oluşturuldu.' })
        onClose()
        navigate(`/products/${id}`)
      }
    } catch (e) {
      setFormError(applyServerErrors(e, setError, FIELDS))
    }
  })

  return (
    <form onSubmit={onSubmit} noValidate>
      <Stack>
        {formError && <Alert color="red">{formError}</Alert>}

        <TextInput label="Ürün adı" required error={errors.name?.message} {...register('name')} />
        <Textarea
          label="Açıklama"
          autosize
          minRows={2}
          error={errors.description?.message}
          {...register('description')}
        />

        <Controller
          control={control}
          name="categoryId"
          render={({ field }) => (
            <Select
              label="Kategori"
              required
              searchable
              data={categoryOptions}
              value={field.value || null}
              onChange={(value) => field.onChange(value ?? '')}
              error={errors.categoryId?.message}
              nothingFoundMessage="Kategori bulunamadı"
            />
          )}
        />

        <Controller
          control={control}
          name="trackingType"
          render={({ field }) => (
            <Radio.Group
              label="Takip tipi"
              required
              value={field.value}
              onChange={(value) => onTrackingTypeChange(value as TrackingType)}
              description={
                trackingTypeLocked
                  ? 'Bu ürünün stoğu veya hareketi olduğu için takip tipi değiştirilemez.'
                  : 'Tekil: her adet ayrı kodla izlenir. Miktar bazlı: lokasyon başına toplam miktar izlenir.'
              }
            >
              <Group mt="xs">
                {(['Serialized', 'Quantity'] as const).map((value) => (
                  <Radio
                    key={value}
                    value={value}
                    label={TRACKING_TYPE_LABELS[value]}
                    disabled={trackingTypeLocked}
                  />
                ))}
              </Group>
            </Radio.Group>
          )}
        />

        <Group grow align="flex-start">
          <Controller
            control={control}
            name="unit"
            render={({ field }) => (
              <Select
                label="Birim"
                required
                data={UNIT_OPTIONS}
                allowDeselect={false}
                value={field.value}
                onChange={(value) => value && field.onChange(value as Unit)}
                disabled={isSerialized}
                description={isSerialized ? 'Tekil takipte birim her zaman adettir.' : undefined}
                error={errors.unit?.message}
              />
            )}
          />
          <Controller
            control={control}
            name="minStockLevel"
            render={({ field }) => (
              <NumberInput
                label="Minimum stok"
                min={0}
                allowNegative={false}
                allowDecimal={!isSerialized}
                decimalScale={3}
                value={field.value ?? ''}
                onChange={(value) => field.onChange(typeof value === 'number' ? value : null)}
                error={errors.minStockLevel?.message}
              />
            )}
          />
        </Group>

        <Controller
          control={control}
          name="isAssignable"
          render={({ field }) => (
            <Checkbox
              label="Zimmetlenebilir"
              description={
                isSerialized
                  ? 'Adetler kullanıcılara zimmetlenebilir.'
                  : 'Sadece tekil takipli ürünler zimmetlenebilir.'
              }
              checked={field.value}
              onChange={(event) => field.onChange(event.currentTarget.checked)}
              disabled={!isSerialized}
            />
          )}
        />

        <Divider label="Barkodlar" labelPosition="left" />
        {product ? (
          <SavedBarcodes product={product} />
        ) : (
          <BarcodeEditor
            barcodes={pendingBarcodes}
            onAdd={async (value: string, format: BarcodeFormat) =>
              setPendingBarcodes((rows) => [...rows, { key: crypto.randomUUID(), value, format }])
            }
            onRemove={async (row) =>
              setPendingBarcodes((rows) => rows.filter((r) => r.key !== row.key))
            }
          />
        )}

        <Group justify="flex-end">
          <Button variant="default" onClick={onClose}>
            Vazgeç
          </Button>
          <Button type="submit" loading={isSubmitting}>
            Kaydet
          </Button>
        </Group>
      </Stack>
    </form>
  )
}

/** Existing products save barcode changes immediately through their own endpoints. */
function SavedBarcodes({ product }: { product: ProductDto }) {
  const addBarcode = useAddBarcode(product.id)
  const removeBarcode = useRemoveBarcode(product.id)
  const [rows, setRows] = useState<BarcodeRow[]>(() =>
    product.barcodes.map((b) => ({ key: b.id, value: b.value, format: b.format })),
  )

  return (
    <Stack gap={4}>
      <Text size="xs" c="dimmed">
        Barkod ekleme ve silme anında kaydedilir.
      </Text>
      <BarcodeEditor
        barcodes={rows}
        onAdd={async (value, format) => {
          try {
            const { id } = await addBarcode.mutateAsync({ value, format })
            setRows((current) => [...current, { key: id, value, format }])
          } catch (e) {
            throw new Error(getErrorMessage(e), { cause: e })
          }
        }}
        onRemove={async (row) => {
          try {
            await removeBarcode.mutateAsync(row.key)
            setRows((current) => current.filter((r) => r.key !== row.key))
          } catch (e) {
            throw new Error(getErrorMessage(e), { cause: e })
          }
        }}
      />
    </Stack>
  )
}
