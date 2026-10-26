import {
  Alert,
  Button,
  Group,
  Stack,
  Stepper,
  Text,
  Textarea,
  TextInput,
  Title,
} from '@mantine/core'
import { notifications } from '@mantine/notifications'
import { useState } from 'react'
import { useNavigate } from 'react-router'
import type { ItemDetail, UserLookup } from '@/api/types'
import { useAssignItem } from '@/features/assignments/assignmentsApi'
import { ItemCard, ItemScanStep } from '@/features/assignments/ItemScanStep'
import { canAssign } from '@/features/assignments/itemRules'
import { usePrefilledItem } from '@/features/assignments/usePrefilledItem'
import { UserPicker } from '@/features/assignments/UserPicker'
import { WizardBar } from '@/features/assignments/WizardBar'
import { emptyErrors } from '@/features/movements/stockInForm'
import { parseLineErrors, type LineErrors } from '@/features/movements/lineErrors'
import { formatDay, todayIstanbul } from '@/lib/dates'

const NOTE_MAX = 500

/** Item → person → due date and note → confirm. Mobile first. */
export function AssignPage() {
  const navigate = useNavigate()
  const assign = useAssignItem()
  const [step, setStep] = useState(0)
  const [item, setItem] = useState<ItemDetail | null>(null)
  const [assignee, setAssignee] = useState<UserLookup | null>(null)
  const [dueDate, setDueDate] = useState('')
  const [note, setNote] = useState('')
  const [errors, setErrors] = useState<LineErrors>(emptyErrors)

  const pick = (picked: ItemDetail) => {
    setItem(picked)
    setStep(1)
  }
  const prefillProblem = usePrefilledItem(canAssign, pick)

  const tomorrow = todayIstanbul(1)
  const dueDateInvalid = dueDate !== '' && dueDate < tomorrow

  const submit = async () => {
    setErrors(emptyErrors)
    try {
      await assign.mutateAsync({
        itemId: item!.id,
        assigneeUserId: assignee!.id,
        dueDate: dueDate || null,
        note: note.trim() || null,
      })
      notifications.show({
        color: 'green',
        message: `${item!.code}, ${assignee!.fullName} adına zimmetlendi.`,
      })
      navigate(`/items/${item!.id}`)
    } catch (e) {
      const parsed = parseLineErrors(e)
      setErrors(parsed)
      if (parsed.fields.assigneeUserId) setStep(1)
      else if (parsed.fields.dueDate || parsed.fields.note) setStep(2)
    }
  }

  return (
    <Stack maw={560} mx="auto" pb={96}>
      <Title order={2}>Zimmet ver</Title>
      <Stepper active={step} size="sm" allowNextStepsSelect={false} onStepClick={setStep}>
        <Stepper.Step label="Adet" description={item?.code} />
        <Stepper.Step label="Kişi" description={assignee?.fullName} />
        <Stepper.Step label="Teslim" />
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
          <ItemScanStep validate={canAssign} onItem={pick} />
        </>
      )}

      {step >= 1 && item && <ItemCard item={item} />}

      {step === 1 && (
        <UserPicker
          label="Zimmeti alacak kişi"
          value={assignee}
          onChange={setAssignee}
          error={errors.fields.assigneeUserId}
          size="md"
        />
      )}

      {step === 2 && (
        <>
          <TextInput
            type="date"
            label="Teslim tarihi (opsiyonel)"
            description="Bu tarihten sonra iade edilmezse gecikmiş sayılır."
            value={dueDate}
            min={tomorrow}
            onChange={(e) => setDueDate(e.currentTarget.value)}
            error={
              errors.fields.dueDate ??
              (dueDateInvalid ? 'Teslim tarihi bugünden sonra olmalıdır.' : undefined)
            }
            size="md"
          />
          <Group gap="xs">
            <Button size="compact-sm" variant="light" onClick={() => setDueDate(todayIstanbul(7))}>
              1 hafta
            </Button>
            <Button size="compact-sm" variant="light" onClick={() => setDueDate(todayIstanbul(30))}>
              1 ay
            </Button>
            {dueDate && (
              <Button
                size="compact-sm"
                variant="subtle"
                color="gray"
                onClick={() => setDueDate('')}
              >
                Tarih yok
              </Button>
            )}
          </Group>
          <Textarea
            label="Not (opsiyonel)"
            value={note}
            onChange={(e) => setNote(e.currentTarget.value)}
            maxLength={NOTE_MAX}
            autosize
            minRows={2}
            error={errors.fields.note}
          />
        </>
      )}

      {step === 3 && item && assignee && (
        <Stack gap={4}>
          <Text>
            <b>{item.code}</b> ({item.productName}) <b>{assignee.fullName}</b> adına zimmetlenecek.
          </Text>
          <Text size="sm" c="dimmed">
            Teslim tarihi: {dueDate ? formatDay(dueDate) : 'yok'}
          </Text>
          {note.trim() && (
            <Text size="sm" c="dimmed">
              Not: {note.trim()}
            </Text>
          )}
          <Text size="sm" c="dimmed">
            Adet bulunduğu lokasyondan çıkar; iade alınana kadar kişinin üzerinde görünür.
          </Text>
        </Stack>
      )}

      {step > 0 && (
        <WizardBar
          onBack={() => setStep(step - 1)}
          onNext={() => (step === 3 ? void submit() : setStep(step + 1))}
          nextDisabled={(step === 1 && !assignee) || (step === 2 && dueDateInvalid)}
          confirm={step === 3}
          nextLabel={step === 3 ? 'Zimmetle' : undefined}
          loading={assign.isPending}
        />
      )}
    </Stack>
  )
}
