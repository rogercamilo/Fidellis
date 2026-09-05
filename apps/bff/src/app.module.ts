import { Module } from '@nestjs/common';
import { ConfigModule } from '@nestjs/config';
import { AuthModule } from './auth/auth.module';
import { DatabaseModule } from './database/database.module';
import { HealthModule } from './health/health.module';
import { OnboardingModule } from './onboarding/onboarding.module';
import { ProxyModule } from './proxy/proxy.module';
import { TenantModule } from './tenant/tenant.module';

@Module({
  imports: [
    ConfigModule.forRoot({ isGlobal: true }),
    DatabaseModule,
    AuthModule,
    TenantModule,
    OnboardingModule,
    HealthModule,
    ProxyModule,
  ],
})
export class AppModule {}
