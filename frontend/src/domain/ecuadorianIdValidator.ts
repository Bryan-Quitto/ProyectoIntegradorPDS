export interface IdValidationResult {
  isValid: boolean;
  type?: 'cedula' | 'ruc_natural' | 'ruc_privada' | 'ruc_publica' | 'invalid';
  error?: string;
}

export function validateEcuadorianId(id: string): IdValidationResult {
  const cleanId = id?.trim() || '';

  if (!cleanId) {
    return { isValid: false, type: 'invalid', error: 'El número de identificación es requerido.' };
  }

  if (!/^\d+$/.test(cleanId)) {
    return { isValid: false, type: 'invalid', error: 'Solo se permiten caracteres numéricos.' };
  }

  if (cleanId.length === 10) {
    return validateCedula(cleanId);
  } else if (cleanId.length === 13) {
    return validateRuc(cleanId);
  }

  return { isValid: false, type: 'invalid', error: 'La identificación debe tener 10 dígitos (cédula) o 13 dígitos (RUC).' };
}

function validateCedula(cedula: string): IdValidationResult {
  const province = parseInt(cedula.substring(0, 2), 10);
  if ((province < 1 || province > 24) && province !== 30) {
    return { isValid: false, type: 'invalid', error: 'Código de provincia inválido.' };
  }

  const thirdDigit = parseInt(cedula.charAt(2), 10);
  if (thirdDigit < 0 || thirdDigit > 5) {
    return { isValid: false, type: 'invalid', error: 'Tercer dígito no corresponde a persona natural.' };
  }

  const coefficients = [2, 1, 2, 1, 2, 1, 2, 1, 2];
  let sum = 0;

  for (let i = 0; i < 9; i++) {
    let val = parseInt(cedula.charAt(i), 10) * coefficients[i];
    if (val >= 10) val -= 9;
    sum += val;
  }

  const verifier = parseInt(cedula.charAt(9), 10);
  const nextTen = Math.ceil(sum / 10) * 10;
  let calculated = nextTen - sum;
  if (calculated === 10) calculated = 0;

  if (calculated !== verifier) {
    return { isValid: false, type: 'invalid', error: 'Dígito verificador de cédula incorrecto.' };
  }

  return { isValid: true, type: 'cedula' };
}

function validateRuc(ruc: string): IdValidationResult {
  if (!ruc.endsWith('001')) {
    return { isValid: false, type: 'invalid', error: 'El RUC debe finalizar en el establecimiento 001.' };
  }

  const baseCedula = ruc.substring(0, 10);
  const cedulaValidation = validateCedula(baseCedula);
  if (cedulaValidation.isValid) {
    return { isValid: true, type: 'ruc_natural' };
  }

  return { isValid: false, type: 'invalid', error: 'El RUC no cumple con el algoritmo de verificación.' };
}
