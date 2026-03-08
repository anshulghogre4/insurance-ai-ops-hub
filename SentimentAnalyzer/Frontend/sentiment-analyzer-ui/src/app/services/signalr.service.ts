import { Injectable, signal, computed, OnDestroy } from '@angular/core';
import { Observable, Subject } from 'rxjs';
import { environment } from '../../environments/environment';

// Dynamic import to handle SSR/test environments where SignalR isn't available
type HubConnection = import('@microsoft/signalr').HubConnection;
type HubConnectionBuilder = import('@microsoft/signalr').HubConnectionBuilder;

@Injectable({ providedIn: 'root' })
export class SignalRService implements OnDestroy {
  private connections = new Map<string, HubConnection>();
  private subjects = new Map<string, Map<string, Subject<unknown>>>();
  private signalRModule: typeof import('@microsoft/signalr') | null = null;

  private _hubStates = signal<Map<string, 'connected' | 'reconnecting' | 'disconnected'>>(new Map());
  readonly connectionState = computed(() => {
    const states = [...this._hubStates().values()];
    if (states.length === 0) return 'disconnected' as const;
    if (states.every(s => s === 'connected')) return 'connected' as const;
    if (states.some(s => s === 'reconnecting')) return 'reconnecting' as const;
    return 'disconnected' as const;
  });
  readonly isConnected = computed(() => this.connectionState() === 'connected');

  private async loadSignalR(): Promise<typeof import('@microsoft/signalr')> {
    if (!this.signalRModule) {
      this.signalRModule = await import('@microsoft/signalr');
    }
    return this.signalRModule;
  }

  async connect(hubPath: string): Promise<void> {
    if (this.connections.has(hubPath)) return;

    const signalR = await this.loadSignalR();
    const url = `${environment.apiUrl}${hubPath}`;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(url)
      .withAutomaticReconnect([0, 2000, 10000, 30000, 60000])
      .build();

    connection.onreconnecting(() => this.setHubState(hubPath, 'reconnecting'));
    connection.onreconnected(() => this.setHubState(hubPath, 'connected'));
    connection.onclose(() => {
      this.setHubState(hubPath, 'disconnected');
      this.connections.delete(hubPath);
    });

    // Register any pre-existing subjects that were created via on() before connect()
    const hubSubjects = this.subjects.get(hubPath);
    if (hubSubjects) {
      for (const [eventName, subject] of hubSubjects) {
        connection.on(eventName, (data: unknown) => subject.next(data));
      }
    }

    await connection.start();
    this.connections.set(hubPath, connection);
    this.setHubState(hubPath, 'connected');
  }

  on<T>(hubPath: string, eventName: string): Observable<T> {
    if (!this.subjects.has(hubPath)) {
      this.subjects.set(hubPath, new Map());
    }

    const hubSubjects = this.subjects.get(hubPath)!;
    if (!hubSubjects.has(eventName)) {
      const subject = new Subject<unknown>();
      hubSubjects.set(eventName, subject);

      // Register handler if connection exists
      const connection = this.connections.get(hubPath);
      if (connection) {
        connection.on(eventName, (data: T) => subject.next(data));
      }
    }

    return hubSubjects.get(eventName)!.asObservable() as Observable<T>;
  }

  async joinGroup(hubPath: string, groupName: string): Promise<void> {
    const connection = this.connections.get(hubPath);
    if (connection) {
      await connection.invoke('JoinSeverityGroup', groupName);
    }
  }

  async leaveGroup(hubPath: string, groupName: string): Promise<void> {
    const connection = this.connections.get(hubPath);
    if (connection) {
      await connection.invoke('LeaveSeverityGroup', groupName);
    }
  }

  async disconnect(hubPath?: string): Promise<void> {
    if (hubPath) {
      const connection = this.connections.get(hubPath);
      if (connection) {
        await connection.stop();
        this.connections.delete(hubPath);
        this.completeSubjects(hubPath);
      }
    } else {
      for (const [path, conn] of this.connections) {
        await conn.stop();
        this.completeSubjects(path);
      }
      this.connections.clear();
      this._hubStates.set(new Map());
    }
  }

  private completeSubjects(hubPath: string): void {
    const hubSubjects = this.subjects.get(hubPath);
    if (hubSubjects) {
      for (const subject of hubSubjects.values()) {
        subject.complete();
      }
      this.subjects.delete(hubPath);
    }
  }

  private setHubState(hubPath: string, state: 'connected' | 'reconnecting' | 'disconnected'): void {
    this._hubStates.update(map => {
      const updated = new Map(map);
      if (state === 'disconnected') {
        updated.delete(hubPath);
      } else {
        updated.set(hubPath, state);
      }
      return updated;
    });
  }

  ngOnDestroy(): void {
    this.disconnect();
  }
}
