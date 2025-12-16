<script setup>
import { onMounted, ref } from 'vue'
import { API_BASE, authHeaders, loadMyPermissions } from '../utils/permissions'

const roles = ref([])
const isAdmin = ref(false)
const name = ref('')
const description = ref('')
const loading = ref(false)
const errorMsg = ref('')

// Gestión de permisos por rol
const selectedRoleId = ref('')
const rolePermissions = ref([]) // [{ resource, operation }]
const resources = ref([])
const operations = ref([])
const newResourceId = ref('')
const newOperationId = ref('')

async function fetchRoles() {
  const res = await fetch(`${API_BASE}/rbac/roles`, { headers: authHeaders() })
  if (!res.ok) throw new Error('No se pudo cargar roles')
  roles.value = await res.json()
}

async function fetchResources() {
  const res = await fetch(`${API_BASE}/rbac/resources`, { headers: authHeaders() })
  if (!res.ok) throw new Error('No se pudo cargar recursos')
  resources.value = await res.json()
}

async function fetchOperations() {
  const res = await fetch(`${API_BASE}/rbac/operations`, { headers: authHeaders() })
  if (!res.ok) throw new Error('No se pudo cargar operaciones')
  operations.value = await res.json()
}

async function loadRolePermissions(roleId) {
  if (!roleId) { rolePermissions.value = []; return }
  const res = await fetch(`${API_BASE}/rbac/roles/${roleId}/permissions`, { headers: authHeaders() })
  if (!res.ok) { rolePermissions.value = []; return }
  const data = await res.json()
  rolePermissions.value = data.permissions || []
}

async function deletePermission(permId) {
  if (!permId || !selectedRoleId.value) return
  if (!confirm('¿Eliminar este permiso del rol?')) return
  try {
    const res = await fetch(`${API_BASE}/rbac/permissions/${permId}`, {
      method: 'DELETE',
      headers: authHeaders()
    })
    if (!res.ok) throw new Error('No autorizado o error')
    await loadRolePermissions(selectedRoleId.value)
  } catch (e) {
    alert('No se pudo eliminar el permiso')
  }
}

onMounted(async () => {
  loading.value = true
  try {
    const { admin } = await loadMyPermissions()
    isAdmin.value = !!admin
    await Promise.all([fetchRoles(), fetchResources(), fetchOperations()])
  } catch (e) {
    errorMsg.value = String(e?.message || 'Error cargando roles')
  } finally {
    loading.value = false
  }
})

async function createRole() {
  try {
    const res = await fetch(`${API_BASE}/rbac/roles`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', ...authHeaders() },
      body: JSON.stringify({ name: name.value, description: description.value || null })
    })
    if (!res.ok) throw new Error('No autorizado o error')
    name.value = ''
    description.value = ''
    await fetchRoles()
  } catch (e) {
    alert('No se pudo crear rol')
  }
}

async function assignPermission() {
  if (!selectedRoleId.value || !newResourceId.value || !newOperationId.value) return
  try {
    const res = await fetch(`${API_BASE}/rbac/assign/permission`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', ...authHeaders() },
      body: JSON.stringify({ roleId: selectedRoleId.value, resourceId: newResourceId.value, operationId: newOperationId.value })
    })
    if (!res.ok) throw new Error('No autorizado o error')
    newResourceId.value = ''
    newOperationId.value = ''
    await loadRolePermissions(selectedRoleId.value)
  } catch (e) {
    alert('No se pudo asignar el permiso')
  }
}
</script>

<template>
  <div class="container">
    <div style="margin-bottom:8px">
      <router-link class="link" to="/">← Volver al inicio</router-link>
    </div>
    <h2 class="title">Roles</h2>
    <p v-if="loading">Cargando...</p>
    <p v-if="errorMsg" style="color:#ef4444">{{ errorMsg }}</p>

    <div class="grid">
      <div>
        <h3>Lista de roles</h3>
        <ul v-if="roles.length" class="list">
          <li v-for="r in roles" :key="r.id">
            <label class="role-option">
              <input type="radio" name="role" :value="r.id" v-model="selectedRoleId" @change="loadRolePermissions(selectedRoleId)" />
              <span class="role-name">{{ r.name }}</span>
              <span v-if="r.description" class="role-desc">— {{ r.description }}</span>
            </label>
          </li>
        </ul>
        <p v-else>No hay roles.</p>

        <div v-if="isAdmin" style="margin-top:12px">
          <h3>Crear rol</h3>
          <input v-model="name" class="input" placeholder="Nombre" />
          <input v-model="description" class="input" placeholder="Descripción (opcional)" />
          <button class="btn" @click="createRole" :disabled="!name">Crear</button>
        </div>
      </div>

      <div>
        <h3>Permisos del rol</h3>
        <p v-if="!selectedRoleId" style="color:#9ca3af">Selecciona un rol para ver sus permisos.</p>
        <table v-else class="table">
          <thead>
            <tr>
              <th>Recurso</th>
              <th>Operación</th>
              <th style="width:120px">Acciones</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="p in rolePermissions" :key="p.id || (p.resource + ':' + p.operation)">
              <td>{{ p.resource }}</td>
              <td>{{ p.operation }}</td>
              <td>
                <button v-if="isAdmin" class="btn danger" @click="deletePermission(p.id)" :disabled="!p.id">Eliminar</button>
              </td>
            </tr>
          </tbody>
        </table>

        <div v-if="isAdmin && selectedRoleId" class="assign">
          <h4>Asignar nuevo permiso</h4>
          <div class="assign-row">
            <select v-model="newResourceId" class="input">
              <option value="" disabled>Selecciona recurso</option>
              <option v-for="r in resources" :key="r.id" :value="r.id">{{ r.name }}</option>
            </select>
            <select v-model="newOperationId" class="input">
              <option value="" disabled>Selecciona operación</option>
              <option v-for="o in operations" :key="o.id" :value="o.id">{{ o.name }}</option>
            </select>
            <button class="btn" @click="assignPermission" :disabled="!newResourceId || !newOperationId">Asignar</button>
          </div>
          <p class="hint">Las asignaciones requieren rol administrador.</p>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.grid { display: grid; grid-template-columns: 1fr 1.5fr; gap: 16px; }
.list { list-style: none; padding: 0; margin: 0; }
.role-option { display: flex; align-items: center; gap: 8px; padding: 8px; border: 1px solid #374151; border-radius: 8px; margin: 6px 0; background: #0b1220; }
.role-name { font-weight: 600; }
.role-desc { color: #9ca3af; margin-left: 6px; }
.table { width: 100%; border-collapse: collapse; margin-top: 8px; }
.table th, .table td { border: 1px solid #374151; padding: 8px; text-align: left; }
.table thead th { background: #0b1220; }
.assign { margin-top: 12px; }
.assign-row { display: grid; grid-template-columns: 1fr 1fr auto; gap: 8px; align-items: center; }
.hint { color: #9ca3af; font-size: 0.9em; }
.input { width: 100%; padding: 10px 12px; border-radius: 8px; border: 1px solid #374151; background: #0b1220; color: #e5e7eb; }
.btn { padding: 10px 12px; border-radius: 8px; background: #3b82f6; color: white; border: none; cursor: pointer; }
.btn.danger { background: #ef4444; }
</style>