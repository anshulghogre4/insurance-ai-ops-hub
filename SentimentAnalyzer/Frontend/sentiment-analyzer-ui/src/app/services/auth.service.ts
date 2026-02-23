import { Injectable, signal, computed } from '@angular/core';
import { createClient, SupabaseClient, User, Session } from '@supabase/supabase-js';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private supabase: SupabaseClient | null = null;

  private _user = signal<User | null>(null);
  private _session = signal<Session | null>(null);
  private _loading = signal<boolean>(true);

  readonly user = this._user.asReadonly();
  readonly session = this._session.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly isAuthenticated = computed(() => !!this._session());
  readonly authEnabled = computed(() => !!environment.supabaseUrl && !!environment.supabaseAnonKey);

  constructor() {
    if (this.authEnabled()) {
      this.supabase = createClient(environment.supabaseUrl, environment.supabaseAnonKey);
      this.initializeAuth();
    } else {
      this._loading.set(false);
    }
  }

  private async initializeAuth(): Promise<void> {
    const { data: { session } } = await this.supabase!.auth.getSession();
    this._session.set(session);
    this._user.set(session?.user ?? null);
    this._loading.set(false);

    this.supabase!.auth.onAuthStateChange((_event, session) => {
      this._session.set(session);
      this._user.set(session?.user ?? null);
    });
  }

  async signUp(email: string, password: string): Promise<{ error: string | null }> {
    if (!this.supabase) return { error: 'Auth not configured' };
    const { error } = await this.supabase.auth.signUp({ email, password });
    return { error: error?.message ?? null };
  }

  async signIn(email: string, password: string): Promise<{ error: string | null }> {
    if (!this.supabase) return { error: 'Auth not configured' };
    const { error } = await this.supabase.auth.signInWithPassword({ email, password });
    return { error: error?.message ?? null };
  }

  async signOut(): Promise<void> {
    if (!this.supabase) return;
    await this.supabase.auth.signOut();
  }

  getAccessToken(): string | null {
    return this._session()?.access_token ?? null;
  }
}
