import React from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { LoginPage } from './presentation/pages/LoginPage';
import { PrivateRoute } from './presentation/components/auth/PrivateRoute';
import { PosTerminalPage } from './presentation/pages/PosTerminalPage';
import { SalesHistoryPage } from './presentation/pages/SalesHistoryPage';

export const App: React.FC = () => {
  return (
    <BrowserRouter>
      <Routes>
        {/* Ruta Pública: Autenticación con Odoo 20 (Fase 4) */}
        <Route path="/login" element={<LoginPage />} />

        {/* Rutas Protegidas: Requiere Vendedor Autenticado con Caja Asignada */}
        <Route element={<PrivateRoute />}>
          {/* Terminal Principal del POS */}
          <Route path="/" element={<PosTerminalPage />} />
          
          {/* Historial de Ventas Sincronizadas y Facturas */}
          <Route path="/historial" element={<SalesHistoryPage />} />
          
          {/* Alias de conveniencia */}
          <Route path="/ventas" element={<Navigate to="/historial" replace />} />
          <Route path="/pos" element={<Navigate to="/" replace />} />
        </Route>

        {/* Redirección por defecto ante rutas desconocidas */}
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
};

export default App;
