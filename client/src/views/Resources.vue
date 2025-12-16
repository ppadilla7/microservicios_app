<script setup>
import { onMounted, ref } from 'vue'
import { API_BASE, authHeaders, loadMyPermissions } from '../utils/permissions'

const resources = ref([])
const isAdmin = ref(false)
const name = ref('')
const description = ref('')
const loading = ref(false)
const errorMsg = ref('')

async function fetchResources() {
  const res = await fetch(`${API_BASE}/rbac/resources`, { headers: authHeaders() })
  if (!res.ok) throw new Error('No se pudo cargar recursos')
  resources.value = await res.json()
}

onMounted(async () => {
  loading.value = true
  try {
    const { admin } = await loadMyPermissions()
    isAdmin.value = !!admin
    await fetchResources()
  } catch (e) {
    errorMsg.value = String(e?.message || 'Error cargando recursos')
  } finally {
    loading.value = false
  }
})

async function createResource() {
  try {
    const res = await fetch(`${API_BASE}/rbac/resources`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', ...authHeaders() },
      body: JSON.stringify({ name: name.value, description: description.value || null })
    })
    if (!res.ok) throw new Error('No autorizado o error')
    name.value = ''
    description.value = ''
    await fetchResources()
  } catch (e) {
    alert('No se pudo crear recurso')
  }
}
</script>

<template>
  <div class="container">
    <div style="margin-bottom:8px">
      <router-link class="link" to="/">← Volver al inicio</router-link>
    </div>
    <h2 class="title">Recursos</h2>
    <p v-if="loading">Cargando...</p>
    <p v-if="errorMsg" style="color:#ef4444">{{ errorMsg }}</p>

    <ul v-if="resources.length">
      <li v-for="r in resources" :key="r.id">{{ r.name }}<span v-if="r.description"> — {{ r.description }}</span></li>
    </ul>
    <p v-else>No hay recursos.</p>

    <div v-if="isAdmin" style="margin-top:12px">
      <h3>Crear recurso</h3>
      <input v-model="name" placeholder="Nombre" />
      <input v-model="description" placeholder="Descripción (opcional)" />
      <button class="btn" @click="createResource" :disabled="!name">Crear</button>
    </div>
  </div>
</template>

<style scoped>
.btn { margin-left: 6px; }
input { margin-right: 6px; }
</style>