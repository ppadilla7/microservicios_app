import { createApp } from 'vue'
import { createRouter, createWebHistory } from 'vue-router'
import App from './App.vue'

import Login from './views/Login.vue'
import Mfa from './views/Mfa.vue'
import AuthCallback from './views/AuthCallback.vue'
import MfaSetup from './views/MfaSetup.vue'
import Home from './views/Home.vue'
import { isLoggedIn, checkPermission } from './utils/permissions'
import Users from './views/Users.vue'
import Roles from './views/Roles.vue'
import Resources from './views/Resources.vue'
import Operations from './views/Operations.vue'
import StudentPanel from './views/StudentPanel.vue'

const routes = [
  { path: '/', component: Home },
  { path: '/login', component: Login },
  { path: '/mfa', component: Mfa },
  { path: '/mfa/setup', component: MfaSetup },
  { path: '/auth/callback', component: AuthCallback },
  // Formularios gestionables protegidos por permisos
  { path: '/users', component: Users, meta: { require: { resource: 'users', operation: 'read' } } },
  { path: '/roles', component: Roles, meta: { require: { resource: 'roles', operation: 'read' } } },
  { path: '/resources', component: Resources, meta: { require: { resource: 'resources', operation: 'read' } } },
  { path: '/operations', component: Operations, meta: { require: { resource: 'operations', operation: 'read' } } },
  { path: '/student-panel', component: StudentPanel, meta: { require: { resource: 'enrollments', operation: 'create' } } },
  
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

router.beforeEach(async (to, from, next) => {
  // Rutas públicas que no requieren token (flujo de login/MFA)
  const publicPaths = ['/login', '/mfa', '/mfa/setup', '/auth/callback']
  if (publicPaths.includes(to.path)) return next()
  if (!isLoggedIn()) return next('/login')
  // Verificar permisos si la ruta los requiere
  const req = to.meta?.require
  if (req && req.resource && req.operation) {
    const allowed = await checkPermission(req.resource, req.operation)
    if (!allowed) return next('/')
  }
  next()
})

createApp(App).use(router).mount('#app')
