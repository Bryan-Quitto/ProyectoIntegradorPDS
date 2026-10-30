import React from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { PosTerminalPage } from './presentation/pages/PosTerminalPage';
import { SalesHistoryPage } from './presentation/pages/SalesHistoryPage';

export const App: React.FC = () => {
  return (
    <BrowserRouter>
      <Routes>
        {/* Terminal Principal del POS */}
        <Route path="/" element={<PosTerminalPage />} />
        
        {/* Historial de Ventas Sincronizadas y Facturas */}
        <Route path="/historial" element={<SalesHistoryPage />} />
        
        {/* Alias de conveniencia */}
        <Route path="/ventas" element={<Navigate to="/historial" replace />} />
        <Route path="/pos" element={<Navigate to="/" replace />} />
        
        {/* Redirección por defecto ante rutas desconocidas */}
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
};

export default App;
