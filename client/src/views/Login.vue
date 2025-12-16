<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'

const router = useRouter()
const email = ref('')
const password = ref('')
const loading = ref(false)
const error = ref('')

import { API_BASE as API } from '../utils/permissions'
import { ensureStudentProfile } from '../utils/apis'

function decodeJwt(token) {
  try {
    const payload = token.split('.')[1]
    const json = JSON.parse(atob(payload.replace(/-/g, '+').replace(/_/g, '/')))
    return json
  } catch { return null }
}

function extractRoles(payload) {
  if (!payload) return []
  const roleClaimUri = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'
  const rolesRaw = payload.role ?? payload[roleClaimUri]
  if (!rolesRaw) return []
  if (Array.isArray(rolesRaw)) return rolesRaw.map(r => String(r).toLowerCase())
  return [String(rolesRaw).toLowerCase()]
}

async function login() {
  loading.value = true
  error.value = ''
  try {
    const res = await fetch(`${API}/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: email.value, password: password.value })
    })
    if (!res.ok) throw new Error('Credenciales inválidas')
    const data = await res.json()
    if (data.mfaRequired) {
      router.push({ path: '/mfa', query: { pendingToken: data.pendingToken } })
    } else if (data.token) {
      localStorage.setItem('token', data.token)
      const payload = decodeJwt(data.token)
      const emailClaim = payload?.email || payload?.unique_name || ''
      const roles = extractRoles(payload)
      if (roles.includes('estudiante')) {
        // Crear perfil si no existe y redirigir a Matrícula
        try { await ensureStudentProfile(emailClaim) } catch {}
        router.push('/student-panel')
      } else {
        router.push('/')
      }
    }
  } catch (e) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}

function loginWithGoogle() {
  window.location.href = `${API}/auth/google`
}
function loginWithGithub() {
  window.location.href = `${API}/auth/github`
}
</script>

<template>
  <div class="login-page">
    <div class="container login-container">
    <h2 class="title">Iniciar sesión</h2>
    <label>Email</label>
    <input class="input" v-model="email" type="email" placeholder="tu@correo.com" />
    <div class="spacer"></div>
    <label>Contraseña</label>
    <input class="input" v-model="password" type="password" placeholder="••••••••" />
    <div class="spacer"></div>
    <button class="btn full" :disabled="loading" @click="login">Entrar</button>
    <div class="spacer"></div>
    <div class="actions">
      <button class="btn secondary" @click="loginWithGoogle">Google</button>
      <button class="btn secondary" @click="loginWithGithub">GitHub</button>
    </div>
    <div v-if="error" style="margin-top:12px;color:#fca5a5">{{ error }}</div>
    </div>
    <div class="below-link">
      <router-link class="link" to="/">Ir a inicio</router-link>
    </div>
  </div>
  
</template>

<style scoped>
.login-page { min-height: 100vh; display: flex; flex-direction: column; align-items: center; justify-content: center; padding: 16px; }
.login-container {
  max-width: 1024px;
  width: 100%;
  margin: 0 auto;
  padding: 32px;
  border: 1px solid #1f2937;
  border-radius: 12px;
  background: #0b1220;
}
.login-container .input { width: 100%; padding-right: 20px; }
.actions { display: grid; grid-template-columns: 1fr 1fr; gap: 8px; }
.actions .btn { width: 100%; }
.full { width: 100%; }
.below-link { text-align: center; margin-top: 12px; }
</style>