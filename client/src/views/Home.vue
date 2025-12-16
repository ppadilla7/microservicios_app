<script setup>
import { onMounted, ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { loadMyPermissions, hasPermissionInMap } from '../utils/permissions'

const router = useRouter()
import { API_BASE as API } from '../utils/permissions'

const email = ref('')
const mfaEnabled = ref(false)
const tokenValue = ref('')
const showToken = ref(false)
const copyMsg = ref('')
const permissionMap = ref({})
const isAdmin = ref(false)
const userRoles = ref([])

function decodeJwt(token) {
  try {
    const payload = token.split('.')[1]
    const json = JSON.parse(atob(payload.replace(/-/g, '+').replace(/_/g, '/')))
    return json
  } catch (e) { return null }
}

function extractRoles(payload) {
  if (!payload) return []
  const roleClaimUri = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'
  const rolesRaw = payload.role ?? payload[roleClaimUri]
  if (!rolesRaw) return []
  if (Array.isArray(rolesRaw)) return rolesRaw.map(r => String(r).toLowerCase())
  return [String(rolesRaw).toLowerCase()]
}

const menuItems = computed(() => {
  const items = []
  const map = permissionMap.value || {}
  const has = (r, o) => !!map?.[String(r).toLowerCase()]?.has(String(o).toLowerCase())
  const admin = isAdmin.value
  const push = (label, path, allowed) => items.push({ label, path, allowed })
  // Visualización basada en permisos y navegación
  push('Usuarios', '/users', admin || has('users','read'))
  push('Roles', '/roles', admin || has('roles','read'))
  push('Recursos', '/resources', admin || has('resources','read'))
  push('Operaciones', '/operations', admin || has('operations','read'))
  // Matrícula disponible si enrollments:create
  push('Matrícula', '/student-panel', admin || has('enrollments','create'))
  // Cursos y Estudiantes siguen siendo visuales si no hay páginas activas
  // Visual no navegable por ahora (sin páginas activas)
  push('Cursos', null, admin || has('courses','read'))
  push('Estudiantes', null, admin || has('students','read'))
  return items
})

const logout = () => {
  localStorage.removeItem('token')
  router.replace('/login')
}

onMounted(async () => {
  const token = localStorage.getItem('token')
  if (!token) {
    router.replace('/login')
    return
  }

  tokenValue.value = token

  try {
    const meRes = await fetch(`${API}/auth/me`, {
      headers: { Authorization: `Bearer ${token}` }
    })
    if (meRes.ok) {
      const me = await meRes.json()
      email.value = me.email || ''
      mfaEnabled.value = !!me.isMfaEnabled
    }
  } catch {}

  // Cargar roles desde el JWT
  const payload = decodeJwt(token)
  const roles = extractRoles(payload)
  isAdmin.value = roles.includes('admin')
  userRoles.value = roles

  // Cargar permisos efectivos
  try {
    const { admin, permissionMap: map } = await loadMyPermissions()
    permissionMap.value = map || {}
    if (admin) isAdmin.value = true
  } catch {}
})

function toggleToken() {
  showToken.value = !showToken.value
}

async function copyToken() {
  try {
    await navigator.clipboard.writeText(tokenValue.value)
    copyMsg.value = 'Copiado'
    setTimeout(() => { copyMsg.value = '' }, 1500)
  } catch {
    copyMsg.value = 'No se pudo copiar'
    setTimeout(() => { copyMsg.value = '' }, 1500)
  }
}
</script>

<template>
  <div class="container">
    <h2 class="title">Inicio</h2>
    <p v-if="email">Usuario: {{ email }}</p>
    <p>MFA: <strong>{{ mfaEnabled ? 'habilitado' : 'no habilitado' }}</strong></p>
    <div v-if="userRoles.length" style="margin-top:8px">
      <p>Mis roles:
        <span v-for="r in userRoles" :key="r" style="background:#1f2937;color:#e5e7eb;padding:4px 8px;border-radius:8px;margin-right:6px">{{ r }}</span>
      </p>
    </div>
    
    <div class="menu">
      <router-link
        v-for="item in menuItems"
        :key="item.label"
        class="menu-item"
        :class="{ disabled: !item.allowed }"
        :to="item.allowed && item.path ? item.path : '#'"
        :aria-disabled="!item.allowed"
        @click.prevent="!item.allowed || !item.path"
      >
        {{ item.label }}
      </router-link>
    </div>

    <button class="btn" @click="logout">Cerrar sesión</button>
    <div class="spacer"></div>
    <router-link v-if="!mfaEnabled" class="link" to="/mfa/setup">Configurar MFA</router-link>

    <div style="margin-top:12px">
      <button class="btn secondary" @click="toggleToken">
        {{ showToken ? 'Ocultar token (debug)' : 'Ver token (debug)' }}
      </button>
      <div v-if="showToken" style="margin-top:8px">
        <textarea readonly rows="4" style="width:100%;font-family:monospace">{{ tokenValue }}</textarea>
        <div style="margin-top:6px">
          <button class="btn secondary" @click="copyToken">Copiar</button>
          <span v-if="copyMsg" style="margin-left:8px;color:#4ade80">{{ copyMsg }}</span>
        </div>
        <p style="margin-top:6px;color:#f59e0b">Uso solo para depuración, no mostrar en producción.</p>
      </div>
    </div>
  </div>
</template>

<style scoped>
.btn { margin: 0.5rem 0 1rem; }
.menu { display: grid; grid-template-columns: repeat(auto-fit, minmax(140px, 1fr)); gap: 8px; margin: 12px 0; }
.menu-item { display: block; background: #1f2937; color: #e5e7eb; padding: 10px; border-radius: 8px; text-align: center; border: 1px solid #374151; }
.menu-item.disabled { opacity: 0.4; filter: grayscale(40%); }
</style>