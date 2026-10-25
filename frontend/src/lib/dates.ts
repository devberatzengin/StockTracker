/** All dates are stored in UTC and shown in Turkey's time zone. */
export const TIME_ZONE = 'Europe/Istanbul'

// Turkey has used UTC+3 all year since 2016.
const ISTANBUL_OFFSET = '+03:00'

const dateTimeFormatter = new Intl.DateTimeFormat('tr-TR', {
  timeZone: TIME_ZONE,
  dateStyle: 'medium',
  timeStyle: 'short',
})

const dateFormatter = new Intl.DateTimeFormat('tr-TR', { timeZone: TIME_ZONE, dateStyle: 'medium' })

const timeFormatter = new Intl.DateTimeFormat('tr-TR', {
  timeZone: TIME_ZONE,
  hour: '2-digit',
  minute: '2-digit',
})

export const formatDateTime = (iso: string) => dateTimeFormatter.format(new Date(iso))

export const formatDate = (iso: string) => dateFormatter.format(new Date(iso))

export const formatTime = (iso: string) => timeFormatter.format(new Date(iso))

/** Start of a calendar day (yyyy-mm-dd) in Istanbul, as an ISO instant for API filters. */
export const startOfDayIstanbul = (day: string) =>
  new Date(`${day}T00:00:00${ISTANBUL_OFFSET}`).toISOString()

/** End of a calendar day (yyyy-mm-dd) in Istanbul, as an ISO instant for API filters. */
export const endOfDayIstanbul = (day: string) =>
  new Date(`${day}T23:59:59.999${ISTANBUL_OFFSET}`).toISOString()
