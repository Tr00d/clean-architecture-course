import { createClient } from "@supabase/supabase-js";
import type { SupabaseClient } from "@supabase/supabase-js";
import type { OperationRecord } from "./operationRecord.ts";

export class OperationsRepository {
  private readonly client: SupabaseClient;

  constructor(url: string, key: string) {
    this.client = createClient(url, key);
  }

  async getOperations(accountId: number): Promise<OperationRecord[]> {
    const { data, error } = await this.client
      .from("operations")
      .select()
      .eq("account_id", accountId)
      .order("date")
      .order("id");

    if (error) {
      throw error;
    }

    return data;
  }

  async insertOperation(accountId: number, amount: number, date: Date): Promise<void> {
    const { error } = await this.client
      .from("operations")
      .insert({ account_id: accountId, amount, date: date.toISOString() });

    if (error) {
      throw error;
    }
  }
}
