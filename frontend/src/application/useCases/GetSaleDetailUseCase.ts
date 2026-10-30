import { IPosApiGateway, SaleOrderDetailDto } from '../ports/IPosApiGateway';

export class GetSaleDetailUseCase {
  private readonly _gateway: IPosApiGateway;

  constructor(gateway: IPosApiGateway) {
    this._gateway = gateway;
  }

  public async execute(id: string): Promise<SaleOrderDetailDto | null> {
    if (!id || !id.trim()) {
      throw new Error('El identificador de la venta es requerido.');
    }
    return await this._gateway.getSaleById(id.trim());
  }
}
