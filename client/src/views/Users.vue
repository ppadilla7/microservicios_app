<script setup>
import { onMounted, ref, computed } from 'vue'
import { API_BASE, authHeaders, loadMyPermissions, checkPermission } from '../utils/permissions'

const users = ref([])
const roles = ref([])
const isAdmin = ref(false)
const canUpdate = ref(false)
const canDelete = ref(false)
const canCreate = ref(false)
const loading = ref(false)
const errorMsg = ref('')

async function fetchUsers() {
  const res = await fetch(`${API_BASE}/auth/users`, { headers: authHeaders() })
  if (!res.ok) throw new Error('No se pudo cargar usuarios')
  users.value = await res.json()
}

async function fetchRoles() {
  const res = await fetch(`${API_BASE}/rbac/roles`, { headers: authHeaders() })
  if (!res.ok) { roles.value = []; return }
  roles.value = await res.json()
}

onMounted(async () => {
  loading.value = true
  try {
    const { admin } = await loadMyPermissions()
    isAdmin.value = !!admin
    canUpdate.value = admin || await checkPermission('users', 'update')
    canDelete.value = admin || await checkPermission('users', 'delete')
    canCreate.value = admin || await checkPermission('users', 'create')
    await Promise.all([fetchUsers(), fetchRoles()])
  } catch (e) {
    errorMsg.value = String(e?.message || 'Error cargando datos')
  } finally {
    loading.value = false
  }
})

async function toggleMfa(u) {
  try {
    const res = await fetch(`${API_BASE}/auth/mfa/toggle`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', ...authHeaders() },
      body: JSON.stringify({ userId: u.id, enable: !u.isMfaEnabled })
    })
    if (!res.ok) throw new Error('No autorizado o error')
    const data = await res.json()
    const idx = users.value.findIndex(x => x.id === data.userId)
    if (idx >= 0) users.value[idx].isMfaEnabled = data.isMfaEnabled
  } catch (e) {
    alert('No se pudo actualizar MFA')
  }
}

async function updateUser(u) {
  const email = prompt('Nuevo email (deja vacío para no cambiar):', u.email || '')
  const password = prompt('Nuevo password (deja vacío para no cambiar):', '')
  try {
    const res = await fetch(`${API_BASE}/auth/users/${u.id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json', ...authHeaders() },
      body: JSON.stringify({ email: email || undefined, password: password || undefined })
    })
    if (!res.ok) throw new Error('No autorizado o error')
    await fetchUsers()
  } catch (e) {
    alert('No se pudo actualizar usuario')
  }
}

async function deleteUser(u) {
  if (!confirm(`Eliminar usuario ${u.email}?`)) return
  try {
    const res = await fetch(`${API_BASE}/auth/users/${u.id}`, {
      method: 'DELETE',
      headers: authHeaders()
    })
    if (!res.ok) throw new Error('No autorizado o error')
    users.value = users.value.filter(x => x.id !== u.id)
  } catch (e) {
    alert('No se pudo eliminar usuario')
  }
}

async function assignRole(u, roleId) {
  try {
    const res = await fetch(`${API_BASE}/rbac/assign/user-role`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', ...authHeaders() },
      body: JSON.stringify({ userId: u.id, roleId })
    })
    if (!res.ok) throw new Error('No autorizado o error')
    await fetchUsers()
  } catch (e) {
    alert('No se pudo asignar rol')
  }
}

// Crear nuevo usuario (si admin o tiene permiso users:create)
const newEmail = ref('')
const newPassword = ref('')
const newEmailError = ref('')
const newPasswordError = ref('')
const createError = ref('')
const showPassword = ref(false)

function isValidEmail(v) { return /\S+@\S+\.\S+/.test(v) }
function isValidPassword(v) { return (v || '').length >= 8 }
const formValid = computed(() => isValidEmail(newEmail.value) && isValidPassword(newPassword.value))
function validateNewUser(showMessages = true) {
  const emailOk = isValidEmail(newEmail.value)
  const passOk = isValidPassword(newPassword.value)
  if (showMessages) {
    newEmailError.value = emailOk ? '' : 'Ingresa un email válido'
    newPasswordError.value = passOk ? '' : 'La contraseña debe tener al menos 8 caracteres'
  }
  return emailOk && passOk
}
async function createUser() {
  try {
    createError.value = ''
    if (!validateNewUser(true)) return
    const res = await fetch(`${API_BASE}/auth/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', ...authHeaders() },
      body: JSON.stringify({ email: newEmail.value, password: newPassword.value })
    })
    if (!res.ok) {
      const t = await res.text()
      throw new Error(t || 'No autorizado o error')
    }
    newEmail.value = ''
    newPassword.value = ''
    newEmailError.value = ''
    newPasswordError.value = ''
    await fetchUsers()
  } catch (e) {
    createError.value = e?.message || 'No se pudo crear usuario'
  }
}
</script>

<template>
  <div class="container">
    <div style="margin-bottom:8px">
      <router-link class="link" to="/">← Volver al inicio</router-link>
    </div>
    <h2 class="title">Usuarios</h2>
    <p v-if="loading">Cargando...</p>
    <p v-if="errorMsg" class="error">{{ errorMsg }}</p>

    <div class="grid">
      <div>
        <h3>Crear nuevo usuario</h3>
        <p v-if="!isAdmin && !canCreate" class="hint">No tienes permiso para crear usuarios.</p>
        <div v-else class="card create-form">
          <div class="form-group">
            <label>Email</label>
            <input v-model="newEmail" class="input" type="email" placeholder="usuario@dominio.com" @blur="validateNewUser(true)" />
            <div class="helper">Usa un correo válido.</div>
            <div v-if="newEmailError" class="error-text">{{ newEmailError }}</div>
          </div>
          <div class="form-group">
            <label>Contraseña</label>
            <div class="password-row">
              <input v-model="newPassword" class="input" :type="showPassword ? 'text' : 'password'" placeholder="••••••••" @blur="validateNewUser(true)" />
              <button type="button" class="btn secondary sm toggle" @click="showPassword = !showPassword">{{ showPassword ? 'Ocultar' : 'Mostrar' }}</button>
            </div>
            <div class="helper">Mínimo 8 caracteres.</div>
            <div v-if="newPasswordError" class="error-text">{{ newPasswordError }}</div>
          </div>
          <button class="btn" @click="createUser" :disabled="!formValid">Crear usuario</button>
          <div v-if="createError" class="error-text" style="margin-top:6px">{{ createError }}</div>
        </div>
      </div>

      <div>
        <table v-if="users.length" class="table">
          <thead>
            <tr>
              <th>Email</th>
              <th>MFA</th>
              <th>Roles</th>
              <th style="width:220px">Acciones</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="u in users" :key="u.id">
              <td class="cell-email" :title="u.email">{{ u.email }}</td>
              <td class="cell-flex">
                <span class="badge" :class="u.isMfaEnabled ? 'ok' : 'warn'">{{ u.isMfaEnabled ? 'habilitado' : 'no habilitado' }}</span>
                <button class="btn secondary sm" @click="toggleMfa(u)">Toggle MFA</button>
              </td>
              <td>
                <div class="chips">
                  <span v-for="r in u.roles" :key="r" class="chip">{{ r }}</span>
                </div>
                <div v-if="isAdmin" class="assign-role">
                  <select v-model="u._roleToAssign" class="input">
                    <option value="" disabled>Selecciona rol</option>
                    <option v-for="r in roles" :key="r.id" :value="r.id">{{ r.name }}</option>
                  </select>
                  <button class="btn secondary sm" @click="assignRole(u, u._roleToAssign)" :disabled="!u._roleToAssign">Asignar</button>
                </div>
              </td>
              <td class="cell-actions">
                <button class="btn sm" @click="updateUser(u)" :disabled="!canUpdate">Actualizar</button>
                <button class="btn danger sm" @click="deleteUser(u)" :disabled="!canDelete">Eliminar</button>
              </td>
            </tr>
          </tbody>
        </table>
        <p v-else>No hay usuarios.</p>
      </div>
    </div>
  </div>
  
</template>

<style scoped>
.grid { display: grid; grid-template-columns: 1fr; gap: 20px; }
.table { width: 100%; border-collapse: collapse; margin-top: 8px; table-layout: fixed; }
.table th, .table td { border: 1px solid #374151; padding: 10px 14px; text-align: left; vertical-align: middle; }
.table thead th { background: #0b1220; }
.table tbody tr:nth-child(odd) { background: #0e1628; }
.table tbody tr:hover { background: #12213a; }
.table th:nth-child(1), .table td:nth-child(1) { width: 30%; }
.table th:nth-child(2), .table td:nth-child(2) { width: 22%; }
.table th:nth-child(3), .table td:nth-child(3) { width: auto; }
.table th:nth-child(4), .table td:nth-child(4) { width: 220px; }
.cell-email { white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.chips { display: flex; flex-wrap: wrap; gap: 6px; }
.chip { background: #1f2937; color: #e5e7eb; padding: 4px 8px; border-radius: 8px; display: inline-block; max-width: 100%; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.cell-flex { display: flex; align-items: center; gap: 8px; flex-wrap: nowrap; }
.cell-actions { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
.cell-actions .btn { flex: 1 1 100px; }
.assign-role { display: grid; grid-template-columns: 1fr auto; gap: 8px; margin-top: 6px; }
.btn { margin-right: 6px; padding: 8px 12px; border-radius: 8px; background: #3b82f6; color: white; border: none; cursor: pointer; width: auto; }
.btn.sm { padding: 6px 10px; font-size: 0.95em; }
.btn.secondary { background: #10b981; color: white; }
.btn.danger { background: #ef4444; color: white; }
.badge { display:inline-block; padding: 4px 8px; border-radius: 8px; font-size: 0.9em; }
.badge.ok { background: #064e3b; color: #a7f3d0; }
.badge.warn { background: #4b5563; color: #e5e7eb; }
.input { width: 100%; padding: 10px 12px; border-radius: 8px; border: 1px solid #374151; background: #0b1220; color: #e5e7eb; }
.card { border: 1px solid #1f2937; border-radius: 12px; padding: 16px; background: #0b1220; }
.create-form { display: grid; grid-template-columns: 1fr; gap: 12px; }
.form-group { display: grid; grid-template-columns: 1fr; gap: 6px; }
.password-row { display: grid; grid-template-columns: 1fr auto; gap: 8px; align-items: center; }
.helper { color: #9ca3af; font-size: 0.85em; }
.error-text { color: #ef4444; font-size: 0.9em; }
.btn.toggle { margin-right: 0; }
.hint { color: #9ca3af; font-size: 0.9em; }
.error { color: #ef4444; }
</style>