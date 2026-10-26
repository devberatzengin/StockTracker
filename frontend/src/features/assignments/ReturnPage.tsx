import { Alert, SegmentedControl, Stack, Stepper, Text, Textarea, Title } from '@mantine/core'
import { notifications } from '@mantine/notifications'
import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router'
import type { ItemDetail, ReturnCondition } from '@/api/types'
import { useReturnAssignment } from '@/features/assignments/assignmentsApi'
import { ItemCard, ItemScanStep } from '@/features/assignments/ItemScanStep'
import { canReturn } from '@/features/assignments/itemRules'
import { RETURN_CONDITION_LABELS } from '@/features/assignments/labels'
import { usePrefilledItem } from '@/features/assignments/usePrefilledItem'
import { WizardBar } from '@/features/assignments/WizardBar'
import { useLocationTree } from '@/features/locations/locationsApi'
import { flattenTree } from '@/features/locations/treeUtils'
import { LocationPicker } from '@/features/movements/LocationPicker'
import { parseLineErrors, type LineErrors } from '@/features/movements/lineErrors'
import { emptyErrors } from '@/features/movements/stockInForm'

const NOTE_MAX = 500

/** Item → return location → condition and note → confirm. A faulty return sends the item to repair. */
export function ReturnPage() {
  const navigate = useNavigate()
  const returnItem = useReturnAssignment()
  const { data: tree = [] } = useLocationTree()
  const [step, setStep] = useState(0)
  const [item, setItem] = useState<ItemDetail | null>(null)
  const [locationId, setLocationId] = useState<string | null>(null)
  const [condition, setCondition] = useState<ReturnCondition>('Good')
  const [note, setNote] = useState('')
  const [errors, setErrors] = useState<LineErrors>(emptyErrors)

  const pick = (picked: ItemDetail) => {
    setItem(picked)
    setStep(1)
  }
  const prefillProblem = usePrefilledItem(canReturn, pick)

  const locationPath = useMemo(
    () => flattenTree(tree).find((f) => f.node.id === locationId)?.pathLabel,
    [tree, locationId],
  )
  const noteMissing = condition === 'Faulty' && note.trim() === ''

  const submit = async () => {
    setErrors(emptyErrors)
    try {
      await returnItem.mutateAsync({
        id: item!.openAssignment!.id,
        returnLocationId: locationId!,
        condition,
        note: note.trim() || null,
      })
      notifications.show({
        color: 'green',
        message:
          condition === 'Faulty'
            ? `${item!.code} iade alındı ve tamire gönderildi.`
            : `${item!.code} iade alındı.`,
      })
      navigate(`/items/${item!.id}`)
    } catch (e) {
      const parsed = parseLineErrors(e)
      setErrors(parsed)
      if (parsed.fields.returnLocationId) setStep(1)
      else if (parsed.fields.note || parsed.fields.condition) setStep(2)
    }
  }

  return (
    <Stack maw={560} mx="auto" pb={96}>
      <Title order={2}>İade al</Title>
      <Stepper active={step} size="sm" allowNextStepsSelect={false} onStepClick={setStep}>
        <Stepper.Step label="Adet" description={item?.code} />
        <Stepper.Step label="Lokasyon" />
        <Stepper.Step label="Durum" />
        <Stepper.Step label="Onay" />
      </Stepper>

      {errors.general.map((text) => (
        <Alert key={text} color="red">
          {text}
        </Alert>
      ))}

      {step === 0 && (
        <>
          {prefillProblem && <Alert color="orange">{prefillProblem}</Alert>}
          <ItemScanStep validate={canReturn} onItem={pick} />
        </>
      )}

      {step >= 1 && item && <ItemCard item={item} />}

      {step === 1 && (
        <LocationPicker
          label="İade lokasyonu"
          value={locationId}
          onChange={setLocationId}
          error={errors.fields.returnLocationId}
          required
        />
      )}

      {step === 2 && (
        <>
          <div>
            <Text size="sm" fw={500} mb={4}>
              Adetin durumu
            </Text>
            <SegmentedControl
              fullWidth
              size="md"
              value={condition}
              onChange={(value) => setCondition(value as ReturnCondition)}
              data={[
                { value: 'Good', label: RETURN_CONDITION_LABELS.Good },
                { value: 'Faulty', label: RETURN_CONDITION_LABELS.Faulty },
              ]}
              color={condition === 'Faulty' ? 'orange' : undefined}
            />
          </div>
          {condition === 'Faulty' && (
            <Alert color="orange">Arızalı iade alınan adet "Tamirde" durumuna geçer.</Alert>
          )}
          <Textarea
            label={condition === 'Faulty' ? 'Arıza notu' : 'Not (opsiyonel)'}
            placeholder={condition === 'Faulty' ? 'Arızayı kısaca açıklayın' : undefined}
            required={condition === 'Faulty'}
            value={note}
            onChange={(e) => setNote(e.currentTarget.value)}
            maxLength={NOTE_MAX}
            autosize
            minRows={2}
            error={errors.fields.note}
          />
        </>
      )}

      {step === 3 && item?.openAssignment && (
        <Stack gap={4}>
          <Text>
            <b>{item.code}</b>, <b>{item.openAssignment.assigneeName}</b> kişisinden iade alınıp{' '}
            <b>{locationPath}</b> lokasyonuna konacak.
          </Text>
          <Text size="sm" c="dimmed">
            Durum: {RETURN_CONDITION_LABELS[condition]}
            {condition === 'Faulty' ? ' (tamire gönderilir)' : ''}
          </Text>
          {note.trim() && (
            <Text size="sm" c="dimmed">
              Not: {note.trim()}
            </Text>
          )}
        </Stack>
      )}

      {step > 0 && (
        <WizardBar
          onBack={() => setStep(step - 1)}
          onNext={() => (step === 3 ? void submit() : setStep(step + 1))}
          nextDisabled={(step === 1 && !locationId) || (step === 2 && noteMissing)}
          confirm={step === 3}
          nextLabel={step === 3 ? 'İade al' : undefined}
          loading={returnItem.isPending}
        />
      )}
    </Stack>
  )
}
