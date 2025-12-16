<script setup>
import { ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'

const route = useRoute()
const router = useRouter()
const code = ref('')
const error = ref('')
const loading = ref(false)
const pendingToken = route.query.pendingToken || ''
import { API_BASE as API } from '../utils/permissions'

async function verify() {
  error.value = ''
  loading.value = true
  try {
    const res = await fetch(`${API}/auth/mfa/verify`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ code: code.value, pendingToken })
    })
    if (!res.ok) throw new Error('Código inválido')
    const data = await res.json()
    localStorage.setItem('token', data.token)
    router.push('/')
  } catch (e) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="container">
    <h2 class="title">Verificación MFA</h2>
    <label>Código TOTP</label>
    <input class="input" v-model="code" type="text" placeholder="000000" />
    <div class="spacer"></div>
    <button class="btn" :disabled="loading" @click="verify">Verificar</button>
    <div v-if="error" style="margin-top:12px;color:#fca5a5">{{ error }}</div>
  </div>
  <div style="text-align:center;margin-top:8px">
    <router-link class="link" to="/">Volver al inicio</router-link>
    <div class="spacer"></div>
    <router-link class="link" to="/login">Volver al login</router-link>
    <div class="spacer"></div>
    <router-link
      v-if="pendingToken"
      class="link"
      :to="{ path: '/mfa/setup', query: { pendingToken } }"
    >Configurar MFA (usar pendingToken)</router-link>
  </div>
</template>