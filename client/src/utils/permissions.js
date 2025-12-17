// Security API configurable vía Vite con fallback al hostname
const HOST = typeof window !== 'undefined' ? window.location.hostname : 'localhost'
export const API_BASE = (import.meta?.env?.VITE_SECURITY_API) || `http://${HOST}:5096`

export function authHeaders() {
  const t = localStorage.getItem('token') || ''
  return t ? { Authorization: `Bearer ${t}` } : {}
}

export async function loadMyPermissions() {
  try {
    const res = await fetch(`${API_BASE}/auth/permissions`, {
      headers: { 'Content-Type': 'application/json', ...authHeaders() }
    })
    if (!res.ok) return { admin: false, permissions: [], permissionMap: {} }
    const data = await res.json()
    const map = {}
    for (const p of data.permissions || []) {
      const r = String(p.resource || '').toLowerCase()
      const o = String(p.operation || '').toLowerCase()
      if (!map[r]) map[r] = new Set()
      map[r].add(o)
    }
    return { admin: !!data.admin, permissions: data.permissions || [], permissionMap: map }
  } catch {
    return { admin: false, permissions: [], permissionMap: {} }
  }
}

export function hasPermissionInMap(permissionMap, resource, operation) {
  const r = String(resource).toLowerCase()
  const o = String(operation).toLowerCase()
  return !!permissionMap?.[r]?.has(o)
}

export function isLoggedIn() {
  return !!localStorage.getItem('token')
}

export async function checkPermission(resource, operation) {
  try {
    const r = encodeURIComponent(String(resource || ''))
    const o = encodeURIComponent(String(operation || ''))
    const res = await fetch(`${API_BASE}/auth/has-permission?resource=${r}&operation=${o}`, {
      headers: { 'Content-Type': 'application/json', ...authHeaders() }
    })
    if (!res.ok) return false
    const data = await res.json()
    return !!data.allowed
  } catch {
    return false
  }
}