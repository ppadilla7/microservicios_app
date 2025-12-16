<script setup>
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'

const router = useRouter()
const loading = ref(false)
const error = ref('')
const secret = ref('')
const otpauth = ref('')
import { API_BASE as API } from '../utils/permissions'

async function setupMfa() {
  error.value = ''
  loading.value = true
  try {
    const token = localStorage.getItem('token')
    let res
    if (token) {
      res = await fetch(`${API}/auth/mfa/setup`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` }
      })
    } else {
      const pendingToken = new URLSearchParams(window.location.search).get('pendingToken')
      if (!pendingToken) {
        throw new Error('Falta token de sesión o pendingToken')
      }
      res = await fetch(`${API}/auth/mfa/setup/pending`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ pendingToken })
      })
    }
    if (!res.ok) {
      const text = await res.text()
      throw new Error(`${res.status} ${text || 'No autorizado o error al generar secreto'}`)
    }
    const data = await res.json()
    secret.value = data.secret
    otpauth.value = data.otpauthUrl
  } catch (e) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  setupMfa()
})
</script>

<template>
  <div class="container">
    <h2 class="title">Configurar MFA</h2>
    <p>Escanea el código QR con Google Authenticator, Microsoft Authenticator, etc.</p>
    <div class="spacer"></div>
    <div v-if="otpauth">
      <img :src="`https://api.qrserver.com/v1/create-qr-code/?size=200x200&data=${encodeURIComponent(otpauth)}`" alt="QR" />
      <div class="spacer"></div>
      <p style="font-size:0.9rem;color:#9ca3af">Secreto TOTP: <strong>{{ secret }}</strong></p>
      <div class="spacer"></div>
      <p>Una vez configurado, vuelve al login y prueba el flujo con MFA.</p>
    </div>
    <div v-if="loading">Generando secreto...</div>
    <div v-if="error" style="margin-top:12px;color:#fca5a5">{{ error }}</div>
  </div>
  <div style="text-align:center;margin-top:8px">
    <router-link class="link" to="/">Ir a inicio</router-link>
  </div>
</template>