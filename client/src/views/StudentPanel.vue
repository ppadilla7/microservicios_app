<script setup>
import { ref, onMounted } from 'vue'
import { getCourses, getStudentByUserId, createEnrollment, createStudent } from '../utils/apis'

const loading = ref(false)
const courses = ref([])
const message = ref('')

// Datos del estudiante
const currentStudent = ref(null)

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
  const rolesRaw = payload?.role ?? payload?.[roleClaimUri]
  if (!rolesRaw) return []
  if (Array.isArray(rolesRaw)) return rolesRaw.map(r => String(r).toLowerCase())
  return [String(rolesRaw).toLowerCase()]
}

function extractUserId(payload) {
  if (!payload) return ''
  const nameIdClaim = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'
  return payload?.sub || payload?.nameid || payload?.[nameIdClaim] || ''
}

function extractEmail(payload) {
  if (!payload) return ''
  return payload?.email || payload?.unique_name || ''
}

function extractFullName(payload, email) {
  if (!payload) return ''
  const name = payload?.name || ''
  if (name) return name
  if (email) return (email.split('@')[0] || email).trim()
  return ''
}

async function bootstrapStudentFromToken() {
  const token = localStorage.getItem('token')
  if (!token) return
  const payload = decodeJwt(token)
  const roles = extractRoles(payload)
  const userId = extractUserId(payload)
  const email = extractEmail(payload)
  const fullName = extractFullName(payload, email)
  if (roles.includes('estudiante') && userId) {
    try {
      let found = await getStudentByUserId(userId)
      if (!found && email && fullName) {
        // Crear automáticamente el perfil si no existe
        found = await createStudent(userId, fullName, email)
      }
      if (found) {
        currentStudent.value = found
        message.value = 'Perfil de estudiante reconocido automáticamente.'
      } else {
        message.value = 'No se encontró perfil de estudiante para este usuario.'
      }
    } catch (e) {
      message.value = e.message || 'Error inicializando perfil de estudiante'
    }
  }
}

async function loadCourses() {
  loading.value = true
  message.value = ''
  try {
    courses.value = await getCourses()
  } catch (e) {
    message.value = e.message || 'Error cargando cursos'
  } finally {
    loading.value = false
  }
}

// Se elimina búsqueda/creación de estudiante desde el panel.

async function enroll(courseId) {
  message.value = ''
  try {
    if (!currentStudent.value?.id) {
      message.value = 'No hay perfil de estudiante vinculado. Inicie sesión como estudiante.'
      return
    }
    const created = await createEnrollment(currentStudent.value.id, courseId)
    message.value = `Matrícula creada: ${created.id}`
  } catch (e) {
    message.value = e.message || 'Error creando matrícula'
  }
}

onMounted(() => {
  loadCourses()
  bootstrapStudentFromToken()
})
</script>

<template>
  <div class="container">
    <h2 class="title">Panel de Estudiante</h2>

    <div class="spacer"></div>
    <h3>Perfil del estudiante</h3>
    <div class="spacer"></div>
    <div v-if="currentStudent">
      <p>Estudiante: <strong>{{ currentStudent.fullName }}</strong> ({{ currentStudent.email }})</p>
    </div>
    <div v-else>
      <p>No hay perfil de estudiante vinculado. Inicie sesión con rol estudiante.</p>
    </div>

    <div class="spacer"></div>
    <h3>Cursos disponibles</h3>
    <p v-if="loading">Cargando cursos...</p>
    <p v-else-if="courses.length === 0">No hay cursos.</p>
    <div v-else>
      <div v-for="c in courses" :key="c.id" style="border:1px solid #374151; padding:12px; border-radius:8px; margin-bottom:8px;">
        <div style="display:flex; justify-content:space-between; align-items:center;">
          <div>
            <div><strong>{{ c.name }}</strong> ({{ c.code }})</div>
            <div>Créditos: {{ c.credits }}</div>
          </div>
          <button class="btn" @click="enroll(c.id)">Matricular</button>
        </div>
      </div>
    </div>

    <div class="spacer"></div>
    <p v-if="message" style="color:#93c5fd">{{ message }}</p>
  </div>
</template>

<style scoped>
h3 { margin: 0 0 8px; }
</style>