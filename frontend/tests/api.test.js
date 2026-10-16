import assert from 'node:assert/strict'
import { afterEach, beforeEach, test } from 'node:test'
import { apiFetch, workspaceStorageKey } from '../src/lib/api.ts'

let requests
let events
let workspace
const originalWindow = globalThis.window

beforeEach(() => {
  requests = []
  events = []
  workspace = 'workspace-a'
  globalThis.window = {
    localStorage: { getItem: key => key === workspaceStorageKey ? workspace : null },
    fetch: async (input, init) => { requests.push({ input, init }); return new Response('{}') },
    dispatchEvent: event => events.push(event.type),
  }
})

afterEach(() => {
  if (originalWindow === undefined) delete globalThis.window
  else globalThis.window = originalWindow
})

test('preserva cabeçalhos e lê o espaço atual a cada requisição', async () => {
  await apiFetch('/api/accounts', { headers: { Accept: 'application/json' } })
  assert.equal(requests[0].init.headers.get('Accept'), 'application/json')
  assert.equal(requests[0].init.headers.get('X-Workspace-Id'), 'workspace-a')
  workspace = 'workspace-b'
  await apiFetch('/api/accounts')
  assert.equal(requests[1].init.headers.get('X-Workspace-Id'), 'workspace-b')
})

test('não inventa um espaço quando não há seleção', async () => {
  workspace = null
  await apiFetch('/api/accounts')
  assert.equal(requests[0].init.headers.has('X-Workspace-Id'), false)
})

test('preserva os cabeçalhos de um objeto Request', async () => {
  await apiFetch(new Request('http://localhost/api/accounts', { headers: { Accept: 'application/json' } }))
  assert.equal(requests[0].init.headers.get('Accept'), 'application/json')
  await apiFetch(new Request('http://localhost/api/accounts'), { headers: { Accept: 'text/plain' } })
  assert.equal(requests[1].init.headers.get('Accept'), 'text/plain')
})

test('repassa o sinal de cancelamento e as opções da requisição', async () => {
  const controller = new AbortController()
  await apiFetch('/api/transactions', { method: 'POST', body: '{}', signal: controller.signal })
  assert.equal(requests[0].init.signal, controller.signal)
  assert.equal(requests[0].init.method, 'POST')
  assert.equal(requests[0].init.body, '{}')
})

test('notifica sessão expirada apenas fora das rotas de autenticação', async () => {
  window.fetch = async () => new Response('{}', { status: 401 })
  await apiFetch('/api/accounts')
  assert.deepEqual(events, ['nexo:unauthorized'])
  events.length = 0
  await apiFetch('/api/auth/login')
  await apiFetch(new URL('http://localhost/api/auth/me'))
  await apiFetch(new Request('http://localhost/api/auth/login'))
  assert.deepEqual(events, [])
})
