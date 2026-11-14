import React, { useState } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { useAuthStore } from '../stores/useAuthStore';
import { loginUseCase } from '@/di';
import { PosApiError } from '@/application/ports/IPosApiGateway';
import { formatUserErrorMessage } from '../utils/errorHandler';
import { Card, CardHeader, CardTitle, CardContent } from '../components/ui/Card';
import { Button } from '../components/ui/Button';
import { Input } from '../components/ui/Input';
import { Store, Lock, User, LogIn, AlertCircle, Loader2, Eye, EyeOff } from 'lucide-react';

const EMAIL_STRICT_REGEX = /^[a-zA-Z0-9.@+_\-]+$/;
const EMAIL_VALIDATION_REGEX = /^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/;

export const LoginPage: React.FC = () => {
  const navigate = useNavigate();
  const location = useLocation();
  const login = useAuthStore((state) => state.login);

  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [loading, setLoading] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const from = (location.state as { from?: { pathname: string } })?.from?.pathname || '/';

  const handleUsernameKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    // Permitir teclas de navegación y control del sistema
    if (
      e.key === 'Backspace' ||
      e.key === 'Tab' ||
      e.key === 'Enter' ||
      e.key === 'Delete' ||
      e.key === 'ArrowLeft' ||
      e.key === 'ArrowRight' ||
      e.key === 'ArrowUp' ||
      e.key === 'ArrowDown' ||
      e.key === 'Home' ||
      e.key === 'End' ||
      e.ctrlKey ||
      e.metaKey
    ) {
      return;
    }

    // Bloquear espacios y caracteres no permitidos en RFC 5321
    if (e.key === ' ' || (e.key.length === 1 && !EMAIL_STRICT_REGEX.test(e.key))) {
      e.preventDefault();
    }
  };

  const handleUsernameChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    // Sanitizar valor: admitir solo caracteres válidos RFC y limitar a 254 caracteres
    const sanitized = e.target.value.replace(/[^a-zA-Z0-9.@+_\-]/g, '').slice(0, 254);
    setUsername(sanitized);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const cleanUsername = username.trim();
    if (!cleanUsername || !password) {
      setErrorMessage('Por favor ingrese su usuario o correo electrónico y contraseña.');
      return;
    }

    // Si tiene formato de correo (@), validar formato estricto RFC 5321
    if (cleanUsername.includes('@') && !EMAIL_VALIDATION_REGEX.test(cleanUsername)) {
      setErrorMessage('El formato de correo electrónico no es válido conforme al estándar RFC.');
      return;
    }

    setLoading(true);
    setErrorMessage(null);

    try {
      const response = await loginUseCase.execute({
        username: cleanUsername,
        password,
      });

      if (response && response.token) {
        login(response.token);
        navigate(from, { replace: true });
      } else {
        setErrorMessage('Credenciales no válidas o acceso denegado.');
      }
    } catch (err: unknown) {
      if (err instanceof PosApiError && err.status === 401) {
        setErrorMessage('Usuario/correo o contraseña incorrectos. Verifique sus credenciales e intente nuevamente.');
      } else {
        const formatted = formatUserErrorMessage(err, 'iniciar sesión');
        setErrorMessage(formatted);
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-slate-100 flex items-center justify-center p-4">
      <div className="w-full max-w-md">
        <div className="text-center mb-6">
          <div className="inline-flex h-16 w-16 rounded-2xl bg-emerald-600 items-center justify-center text-white shadow-lg mb-3">
            <Store className="h-8 w-8" />
          </div>
          <h1 className="text-2xl font-bold text-slate-900 tracking-tight">
            Punto de Venta POS &bull; Odoo 20
          </h1>
          <p className="text-sm text-slate-500 mt-1">
            Autenticación de Vendedores y Asignación de Caja
          </p>
        </div>

        <Card className="shadow-md border-slate-200 bg-white">
          <CardHeader className="pb-4 border-b border-slate-100">
            <CardTitle className="text-lg font-bold text-slate-800 flex items-center gap-2">
              <LogIn className="h-5 w-5 text-emerald-600" />
              Iniciar Sesión
            </CardTitle>
          </CardHeader>
          <CardContent className="pt-5">
            {errorMessage && (
              <div
                className="mb-5 p-3.5 bg-rose-50 border border-rose-200 rounded-lg flex items-start gap-3 text-rose-700 text-sm animate-in fade-in duration-200"
                role="alert"
              >
                <AlertCircle className="h-5 w-5 shrink-0 text-rose-600 mt-0.5" />
                <div className="flex-1 font-medium">{errorMessage}</div>
              </div>
            )}

            <form onSubmit={handleSubmit} className="space-y-4">
              <div>
                <label className="block text-xs font-semibold text-slate-700 uppercase tracking-wider mb-1.5">
                  Correo Electrónico
                </label>
                <div className="relative">
                  <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none text-slate-400">
                    <User className="h-4 w-4" />
                  </div>
                  <Input
                    type="text"
                    required
                    autoFocus
                    maxLength={254}
                    value={username}
                    onKeyDown={handleUsernameKeyDown}
                    onChange={handleUsernameChange}
                    placeholder="ej. cajero1@gmail.com"
                    className="pl-9"
                    disabled={loading}
                  />
                </div>
              </div>

              <div>
                <label className="block text-xs font-semibold text-slate-700 uppercase tracking-wider mb-1.5">
                  Contraseña ERP
                </label>
                <div className="relative">
                  <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none text-slate-400">
                    <Lock className="h-4 w-4" />
                  </div>
                  <Input
                    type={showPassword ? 'text' : 'password'}
                    required
                    maxLength={255}
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    placeholder="••••••••••••"
                    className="pl-9"
                    disabled={loading}
                    rightElement={
                      <button
                        type="button"
                        onClick={() => setShowPassword((prev) => !prev)}
                        className="focus:outline-none p-1 rounded hover:bg-slate-100 transition-colors cursor-pointer"
                        aria-label={showPassword ? 'Ocultar contraseña' : 'Ver contraseña'}
                        tabIndex={-1}
                      >
                        {showPassword ? (
                          <EyeOff className="h-4 w-4 text-slate-500 hover:text-slate-700" />
                        ) : (
                          <Eye className="h-4 w-4 text-slate-500 hover:text-slate-700" />
                        )}
                      </button>
                    }
                  />
                </div>
              </div>

              <div className="pt-2">
                <Button
                  type="submit"
                  disabled={loading}
                  className="w-full bg-emerald-600 hover:bg-emerald-700 text-white font-medium py-2.5 rounded-lg shadow-sm flex items-center justify-center gap-2 transition-colors"
                >
                  {loading ? (
                    <>
                      <Loader2 className="h-4 w-4 animate-spin" />
                      <span>Validando en Odoo 20...</span>
                    </>
                  ) : (
                    <>
                      <LogIn className="h-4 w-4" />
                      <span>Ingresar al POS</span>
                    </>
                  )}
                </Button>
              </div>
            </form>

            <div className="mt-6 pt-4 border-t border-slate-100 text-center">
              <p className="text-xs text-slate-500 leading-relaxed">
                Acceso para vendedores registrados en el ERP Odoo con caja activa asignada.
              </p>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
};
