# TASK-026 Brevo transactional mail transport

Status: DONE

Add an outbound adapter for transactional email using the [Brevo email API](https://developers.brevo.com/reference/send-transac-email). Send one recipient per request, keep authentication in the request header, set bounded connection and response times, and disallow redirects and automatic application retries. Distinguish provider acceptance from rejection and an unknown outcome. Acceptance is not evidence that the recipient received the message.

Verify the request and outcome mapping, including cancellation, with a fake HTTP handler. This increment does not wire the adapter to account routes or enable delivery in any environment. Bind runtime credentials through the application's secret configuration and verify delivery only when a designated test recipient is agreed.
