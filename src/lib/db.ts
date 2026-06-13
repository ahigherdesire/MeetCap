import { openDB, type DBSchema, type IDBPDatabase } from 'idb'
import type { StoredRecording, RecordingMeta } from './types'

interface MeetCapDB extends DBSchema {
  recordings: {
    key: string
    value: StoredRecording
    indexes: { 'by-createdAt': number }
  }
}

const DB_NAME = 'meetcap'
const DB_VERSION = 1

let dbPromise: Promise<IDBPDatabase<MeetCapDB>> | null = null

function getDB() {
  if (!dbPromise) {
    dbPromise = openDB<MeetCapDB>(DB_NAME, DB_VERSION, {
      upgrade(db) {
        const store = db.createObjectStore('recordings', { keyPath: 'id' })
        store.createIndex('by-createdAt', 'createdAt')
      },
    })
  }
  return dbPromise
}

export async function saveRecording(rec: StoredRecording): Promise<void> {
  const db = await getDB()
  await db.put('recordings', rec)
}

export async function listRecordings(): Promise<RecordingMeta[]> {
  const db = await getDB()
  const all = await db.getAllFromIndex('recordings', 'by-createdAt')
  // Newest first; strip the blob for list views to keep memory low.
  return all
    .sort((a, b) => b.createdAt - a.createdAt)
    .map(({ blob: _blob, ...meta }) => meta)
}

export async function getRecording(id: string): Promise<StoredRecording | undefined> {
  const db = await getDB()
  return db.get('recordings', id)
}

export async function renameRecording(id: string, name: string): Promise<void> {
  const db = await getDB()
  const rec = await db.get('recordings', id)
  if (!rec) return
  rec.name = name
  await db.put('recordings', rec)
}

export async function deleteRecording(id: string): Promise<void> {
  const db = await getDB()
  await db.delete('recordings', id)
}

export interface StorageEstimate {
  usage: number
  quota: number
}

export async function estimateStorage(): Promise<StorageEstimate | null> {
  if (navigator.storage?.estimate) {
    const { usage = 0, quota = 0 } = await navigator.storage.estimate()
    return { usage, quota }
  }
  return null
}
