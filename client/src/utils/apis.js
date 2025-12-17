// Endpoints configurables vía variables de entorno Vite con fallback al hostname
const HOST = typeof window !== 'undefined' ? window.location.hostname : 'localhost'
export const COURSES_API = (import.meta?.env?.VITE_COURSES_API) || `http://${HOST}:5202`
export const ENROLLMENTS_API = (import.meta?.env?.VITE_ENROLLMENT_API) || `http://${HOST}:5281`
export const STUDENTS_API = (import.meta?.env?.VITE_STUDENTS_API) || `http://${HOST}:5084`

export async function getCourses() {
  const res = await fetch(`${COURSES_API}/api/Courses`)
  if (!res.ok) throw new Error('No se pudo cargar cursos')
  return await res.json()
}

export async function listStudents() {
  const res = await fetch(`${STUDENTS_API}/api/Students`)
  if (!res.ok) throw new Error('No se pudo cargar estudiantes')
  return await res.json()
}

export async function getStudentByEmail(email) {
  const res = await fetch(`${STUDENTS_API}/api/Students/by-email/${encodeURIComponent(email)}`)
  if (res.status === 404) return null
  if (!res.ok) throw new Error('No se pudo obtener estudiante por email')
  return await res.json()
}

export async function getStudentByUserId(userId) {
  const res = await fetch(`${STUDENTS_API}/api/Students/by-user/${encodeURIComponent(userId)}`)
  if (res.status === 404) return null
  if (!res.ok) throw new Error('No se pudo obtener estudiante por userId')
  return await res.json()
}

// Nota: el backend ahora requiere userId para crear estudiantes.
export async function createStudent(userId, fullName, email) {
  const res = await fetch(`${STUDENTS_API}/api/Students`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ userId, fullName, email })
  })
  if (!res.ok) throw new Error('No se pudo crear estudiante')
  return await res.json()
}

export async function createEnrollment(studentId, courseId) {
  const res = await fetch(`${ENROLLMENTS_API}/api/Enrollments`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ studentId, courseId })
  })
  if (!res.ok) throw new Error('No se pudo crear la matrícula')
  return await res.json()
}

// Ya no se crea perfil desde el cliente; solo verificación por email.
export async function ensureStudentProfile(email) {
  if (!email) return false
  try {
    const found = await getStudentByEmail(email)
    return !!found
  } catch {
    return false
  }
}