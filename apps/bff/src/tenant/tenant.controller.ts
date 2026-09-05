import { Body, Controller, Get, Headers, Post, UnauthorizedException } from '@nestjs/common';
import { IsString, MinLength } from 'class-validator';
import { TenantService } from './tenant.service';

class SelectTenantDto {
  @IsString()
  @MinLength(1)
  tenant!: string;
}

@Controller('tenants')
export class TenantController {
  constructor(private readonly tenants: TenantService) {}

  @Get()
  list(@Headers('authorization') authorization?: string) {
    return this.tenants.listForToken(this.token(authorization));
  }

  @Post('select')
  select(@Body() dto: SelectTenantDto, @Headers('authorization') authorization?: string) {
    return this.tenants.select(this.token(authorization), dto.tenant);
  }

  private token(authorization?: string): string {
    const token = authorization?.replace(/^Bearer\s+/i, '').trim();
    if (!token) throw new UnauthorizedException('Sem token.');
    return token;
  }
}
