<script setup>
</script>

<template>
  <router-view />
  
</template>

<style>
html, body, #app { height: 100%; }
body {
  margin: 0;
  font-family: system-ui, -apple-system, Segoe UI, Roboto, Ubuntu, Cantarell, Noto Sans, sans-serif;
  background: #0f172a;
  color: #e2e8f0;
}
.container {
  max-width: 1200px;
  margin: 48px auto;
  background: #111827;
  padding: 40px;
  border-radius: 12px;
  box-shadow: 0 10px 30px rgba(0,0,0,0.3);
}
.input {
  width: 100%;
  padding: 10px 12px;
  border-radius: 8px;
  border: 1px solid #374151;
  background: #0b1220;
  color: #e5e7eb;
}
.btn {
  width: 100%;
  padding: 10px 12px;
  border-radius: 8px;
  background: #3b82f6;
  color: white;
  border: none;
  cursor: pointer;
}
.btn.secondary { background: #10b981; }
.actions { display: grid; grid-template-columns: 1fr 1fr; gap: 8px; }
.title { margin: 0 0 16px; }
label { display:block; margin-bottom: 6px; font-size: 0.9rem; color: #9ca3af; }
.spacer { height: 12px; }
.link { color: #93c5fd; text-decoration: none; }
</style>
