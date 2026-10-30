import { PosApiHttpAdapter } from '@/infrastructure/http/PosApiHttpAdapter';
import {
  GetSalesHistoryUseCase,
  CheckoutSaleUseCase,
  SearchCatalogUseCase,
  GetActiveSessionUseCase,
  ManageCartUseCase,
} from '@/application/useCases/UseCases';

// ============================================================================
// COMPOSITION ROOT (Main Component / Inversión de Control)
// Referencia: Clean Architecture (Robert C. Martin) - Secciones 1.7 y 2.2
//
// Este archivo actúa como el punto de composición único del frontend donde se
// instancian las implementaciones concretas de infraestructura y se inyectan
// en los casos de uso. De esta forma, la capa de presentación (hooks y componentes)
// queda 100% desacoplada de la infraestructura HTTP/API.
// ============================================================================

// Adaptador concreto de infraestructura HTTP
export const apiAdapter = new PosApiHttpAdapter();

// Casos de uso ensamblados y listos para ser consumidos por la capa de presentación
export const searchCatalogUseCase = new SearchCatalogUseCase(apiAdapter);
export const getActiveSessionUseCase = new GetActiveSessionUseCase(apiAdapter);
export const getSalesHistoryUseCase = new GetSalesHistoryUseCase(apiAdapter);
export const checkoutSaleUseCase = new CheckoutSaleUseCase(apiAdapter);
export const manageCartUseCase = new ManageCartUseCase();
