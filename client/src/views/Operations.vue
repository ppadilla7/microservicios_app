<script setup>
import { onMounted, ref } from 'vue'
import { API_BASE, authHeaders, loadMyPermissions } from '../utils/permissions'

const operations = ref([])
const isAdmin = ref(false)
const name = ref('')
const description = ref('')
const loading = ref(false)
const errorMsg = ref('')

async function fetchOperations() {
  const res = await fetch(`${API_BASE}/rbac/operations`, { headers: authHeaders() })
  if (!res.ok) throw new Error('No se pudo cargar operaciones')
  operations.value = await res.json()
}

onMounted(async () => {
  loading.value = true
  try {
    const { admin } = await loadMyPermissions()
    isAdmin.value = !!admin
    await fetchOperations()
  } catch (e) {
    errorMsg.value = String(e?.message || 'Error cargando operaciones')
  } finally {
    loading.value = false
  }
})

async function createOperation() {
  try {
    const res = await fetch(`${API_BASE}/rbac/operations`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', ...authHeaders() },
      body: JSON.stringify({ name: name.value, description: description.value || null })
    })
    if (!res.ok) throw new Error('No autorizado o error')
    name.value = ''
    description.value = ''
    await fetchOperations()
  } catch (e) {
    alert('No se pudo crear operación')
  }
}
</script>

<template>
  <div class="container">
    <div style="margin-bottom:8px">
      <router-link class="link" to="/">← Volver al inicio</router-link>
    </div>
    <h2 class="title">Operaciones</h2>
    <p v-if="loading">Cargando...</p>
    <p v-if="errorMsg" style="color:#ef4444">{{ errorMsg }}</p>

    <ul v-if="operations.length">
      <li v-for="o in operations" :key="o.id">{{ o.name }}<span v-if="o.description"> — {{ o.description }}</span></li>
    </ul>
    <p v-else>No hay operaciones.</p>

    <div v-if="isAdmin" style="margin-top:12px">
      <h3>Crear operación</h3>
      <input v-model="name" placeholder="Nombre" />
      <input v-model="description" placeholder="Descripción (opcional)" />
      <button class="btn" @click="createOperation" :disabled="!name">Crear</button>
    </div>
  </div>
</template>

<style scoped>
.btn { margin-left: 6px; }
input { margin-right: 6px; }
</style>