import { Body, Controller, Get, Param, Post } from '@nestjs/common';
import { IsOptional, IsString, MinLength } from 'class-validator';
import { InvitationsService } from './invitations.service';

class AcceptDto {
  @IsString()
  @MinLength(6)
  password!: string;

  @IsOptional()
  @IsString()
  displayName?: string;
}

/**
 * Aceite público de convite (D-01). Não exige autenticação — o próprio token do link é a credencial.
 * O `describe` alimenta a tela de aceite; o `accept` cria a conta + membership e faz auto-login.
 */
@Controller('invitations')
export class InvitationsController {
  constructor(private readonly invitations: InvitationsService) {}

  @Get(':token')
  describe(@Param('token') token: string) {
    return this.invitations.describe(token);
  }

  @Post(':token/accept')
  accept(@Param('token') token: string, @Body() dto: AcceptDto) {
    return this.invitations.accept({ token, password: dto.password, displayName: dto.displayName });
  }
}
