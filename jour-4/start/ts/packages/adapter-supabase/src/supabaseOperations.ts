import { createClient } from "@supabase/supabase-js";
import type { SupabaseClient } from "@supabase/supabase-js";
import { Account, Amount } from "@bank/domain";
import type { AccountId, Operations } from "@bank/domain";
import type { OperationRecord } from "./operationRecord.ts";

export class SupabaseOperations implements Operations {
  private readonly client: SupabaseClient;

  constructor(url: string, key: string) {
    this.client = createClient(url, key);
  }

  async accountOf(accountId: AccountId): Promise<Account> {
    const { data, error } = await this.client
      .from("operations")
      .select()
      .eq("account_id", accountId.value)
      .order("date")
      .order("id");

    if (error) {
      throw error;
    }

    const records: OperationRecord[] = data;
    const history = records.map((record) => ({
      amount: new Amount(record.amount),
      date: new Date(record.date)
    }));

    return new Account(accountId, history);
  }

  async save(account: Account): Promise<void> {
    const records = account.newOperations.map((operation) => ({
      account_id: account.id.value,
      amount: operation.amount.value,
      date: operation.date.toISOString()
    }));

    const { error } = await this.client.from("operations").insert(records);

    if (error) {
      throw error;
    }
  }
}
