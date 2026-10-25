/**
 * Case suffixes for names in Turkish sentences ("Raf A1'den", "Vida M4'ü", "Raf B2'ye").
 * Names are treated as proper nouns, so the suffix follows an apostrophe. Vowel harmony and the
 * voiceless consonant rule use how the end of the name is pronounced; trailing numbers are read
 * out ("A1" ends like "bir", "M4" like "dört").
 */

const VOWELS = 'aeıioöuü'
const BACK_VOWELS = 'aıou'
const ROUNDED_VOWELS = 'oöuü'
// "fıstıkçı şahap": voiceless consonants turn d into t.
const VOICELESS = 'fstkçşhp'

const ONES = ['', 'bir', 'iki', 'üç', 'dört', 'beş', 'altı', 'yedi', 'sekiz', 'dokuz']
const TENS = ['', 'on', 'yirmi', 'otuz', 'kırk', 'elli', 'altmış', 'yetmiş', 'seksen', 'doksan']

/** The word whose sound decides the suffix: the last word, with a trailing number read out. */
function spokenEnding(name: string): string {
  const trimmed = name.trim()
  const number = /(\d+)$/.exec(trimmed)
  if (number) {
    const n = Number(number[1].slice(-7))
    if (n === 0) return 'sıfır'
    if (n % 10 !== 0) return ONES[n % 10]
    if (n % 100 !== 0) return TENS[(n % 100) / 10]
    if (n % 1000 !== 0) return 'yüz'
    if (n % 1_000_000 !== 0) return 'bin'
    return 'milyon'
  }
  return (trimmed.split(/\s+/).at(-1) ?? '').toLocaleLowerCase('tr-TR')
}

interface Sound {
  lastVowel: string
  endsWithVowel: boolean
  endsVoiceless: boolean
}

function sound(name: string): Sound {
  const word = spokenEnding(name)
  const letters = [...word].filter((c) => /\p{L}/u.test(c))
  const last = letters.at(-1) ?? ''
  // Words without vowels (e.g. "SSD") default to front, unrounded harmony.
  const lastVowel = [...letters].reverse().find((c) => VOWELS.includes(c)) ?? 'e'
  return {
    lastVowel,
    endsWithVowel: VOWELS.includes(last),
    endsVoiceless: VOICELESS.includes(last),
  }
}

const isBack = (vowel: string) => BACK_VOWELS.includes(vowel)

function fourWay(vowel: string) {
  if (ROUNDED_VOWELS.includes(vowel)) return isBack(vowel) ? 'u' : 'ü'
  return isBack(vowel) ? 'ı' : 'i'
}

/** Accusative (belirtme): Laptop'u, Vida M4'ü, Kutu'yu. */
export function accusative(name: string) {
  const s = sound(name)
  return `${name}'${s.endsWithVowel ? 'y' : ''}${fourWay(s.lastVowel)}`
}

/** Dative (yönelme): Raf A1'e, Raf B2'ye, Depo'ya. */
export function dative(name: string) {
  const s = sound(name)
  return `${name}'${s.endsWithVowel ? 'y' : ''}${isBack(s.lastVowel) ? 'a' : 'e'}`
}

/** Ablative (ayrılma): Raf A1'den, Kutu 4'ten, Depo'dan. */
export function ablative(name: string) {
  const s = sound(name)
  return `${name}'${s.endsVoiceless ? 't' : 'd'}${isBack(s.lastVowel) ? 'a' : 'e'}n`
}
