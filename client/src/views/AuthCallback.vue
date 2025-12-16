<script setup>
import { onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'

const router = useRouter()
const route = useRoute()

onMounted(() => {
  const token = route.query.token
  const pendingToken = route.query.pendingToken
  const mfaRequired = route.query.mfaRequired === 'true' || !!pendingToken

  // Si MFA es requerido, no guardar token y redirigir a verificación MFA
  if (mfaRequired) {
    // Asegurar que no quede un token previo
    localStorage.removeItem('token')
    router.replace({ path: '/mfa', query: { pendingToken: pendingToken || '' } })
    return
  }

  // Si no hay MFA requerido y hay token válido, guardarlo y redirigir al inicio
  if (token) {
    localStorage.setItem('token', token)
    router.replace('/')
    return
  }

  // Si no hay credenciales, volver al login
  router.replace('/login')
})
</script>

<template>
  <div class="container">
    <h2 class="title">Procesando autenticación...</h2>
    <p>Redirigiendo</p>
  </div>
  <div style="text-align:center;margin-top:8px">
    <router-link class="link" to="/">Ir a inicio</router-link>
  </div>
</template>