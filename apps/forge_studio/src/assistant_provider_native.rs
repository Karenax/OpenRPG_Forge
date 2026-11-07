//! Trusted native provider transport. The browser/model never chooses a URL/key.
use forge_project::assistant::provider::{
    ApiKind, MAX_PROVIDER_OUTPUT_TOKENS, MAX_PROVIDER_RESPONSE_BYTES, OutboundApproval,
    ProviderRequest, ProviderResult,
};
use reqwest::{Url, blocking::Client, redirect::Policy};
use std::{io::Read, net::IpAddr, time::Duration};

/// Credentials deliberately have no Debug/Serialize implementation.
#[derive(Clone)]
pub struct NativeProvider {
    endpoint: Url,
    model: String,
    kind: ApiKind,
    api_key: Option<String>,
    client: Client,
}

impl NativeProvider {
    /// Native configuration only. Plain HTTP is limited to literal LAN/loopback
    /// addresses; remote providers require TLS with normal certificate checks.
    pub fn new(
        base_url: &str,
        model: &str,
        kind: ApiKind,
        api_key: Option<String>,
    ) -> Result<Self, String> {
        let mut endpoint = Url::parse(base_url).map_err(|_| "Invalid provider URL")?;
        if !endpoint.username().is_empty()
            || endpoint.password().is_some()
            || endpoint.query().is_some()
            || endpoint.fragment().is_some()
            || !endpoint.path().trim_end_matches('/').ends_with("/v1")
            || model.is_empty()
            || model.len() > 128
            || model.trim() != model
            || model.chars().any(char::is_control)
        {
            return Err("Provider requires a clean /v1 base URL and a bounded model name".into());
        }
        match endpoint.scheme() {
            "https" => (),
            "http" if local_host(&endpoint) => (),
            _ => {
                return Err(
                    "Provider requires HTTPS or an explicitly configured local HTTP address".into(),
                );
            }
        }
        if api_key.as_ref().is_some_and(|key| {
            key.is_empty() || key.len() > 8192 || key.chars().any(char::is_control)
        }) {
            return Err("Invalid native provider credential".into());
        }
        let suffix = match kind {
            ApiKind::ChatCompletions => "chat/completions",
            ApiKind::Responses => "responses",
        };
        endpoint.set_path(&format!(
            "{}/{suffix}",
            endpoint.path().trim_end_matches('/')
        ));
        let client = Client::builder()
            .redirect(Policy::none())
            .retry(reqwest::retry::never())
            .no_proxy()
            .connect_timeout(Duration::from_secs(10))
            .timeout(Duration::from_secs(120))
            .build()
            .map_err(|_| "Cannot initialize native provider transport")?;
        Ok(Self {
            endpoint,
            model: model.into(),
            kind,
            api_key,
            client,
        })
    }

    /// One request only. Retry/budget decisions belong to the persisted job ledger.
    #[must_use]
    pub fn destination(&self) -> &str {
        self.endpoint.as_str()
    }

    /// Long, explicitly budgeted plans need more time, especially with reasoning.
    /// The per-request bound stays within 2–16 minutes; this never enables retries.
    #[must_use]
    pub fn request_timeout_seconds(maximum_output_tokens: u64) -> u64 {
        120 * maximum_output_tokens
            .clamp(128, u64::from(MAX_PROVIDER_OUTPUT_TOKENS))
            .div_ceil(2048)
    }

    pub fn generate(
        &self,
        request: &ProviderRequest,
        approval: &OutboundApproval,
    ) -> Result<ProviderResult, String> {
        request.verify_outbound(approval)?;
        if request.kind() != self.kind
            || request.model() != self.model
            || request.destination() != self.endpoint.as_str()
        {
            return Err("Provider configuration differs from the displayed request".into());
        }
        let mut builder = self
            .client
            .post(self.endpoint.clone())
            .json(request.body())
            .timeout(Duration::from_secs(Self::request_timeout_seconds(
                u64::from(request.maximum_output_tokens()),
            )));
        if let Some(key) = &self.api_key {
            builder = builder.bearer_auth(key);
        }
        let response = builder
            .send()
            .map_err(|_| "Provider request failed or timed out; result may be uncertain")?;
        if !response.status().is_success() {
            return Err(provider_http_error(response));
        }
        if response
            .content_length()
            .is_some_and(|length| length > MAX_PROVIDER_RESPONSE_BYTES as u64)
        {
            return Err("Provider response exceeds 1 MiB".into());
        }
        let mut bytes = Vec::new();
        response
            .take(MAX_PROVIDER_RESPONSE_BYTES as u64 + 1)
            .read_to_end(&mut bytes)
            .map_err(|_| "Provider response was interrupted")?;
        request.parse_response(&bytes)
    }
}

fn provider_http_error(response: reqwest::blocking::Response) -> String {
    let status = response.status().as_u16();
    let mut bytes = Vec::new();
    if response.take(8193).read_to_end(&mut bytes).is_ok() {
        return provider_error_message(status, &bytes);
    }
    format!("Provider HTTP {status}; response body withheld")
}

fn provider_error_message(status: u16, bytes: &[u8]) -> String {
    if bytes.len() <= 8192
        && let Ok(value) = serde_json::from_slice::<serde_json::Value>(bytes)
    {
        let error = &value["error"];
        if (error["code"] == "structured_output_failed"
            || error["type"] == "structured_output_failed")
            && error["message"]
                .as_str()
                .is_some_and(|message| message.to_ascii_lowercase().contains("length"))
        {
            return "Le serveur signale que le plafond de sortie a été atteint avant la fin du plan. Augmenter ce plafond dans la configuration du fournisseur, puis préparer une nouvelle demande. Aucun résultat n’a été appliqué.".into();
        }
    }
    format!("Provider HTTP {status}; response body withheld")
}

fn local_host(url: &Url) -> bool {
    let Some(host) = url.host_str() else {
        return false;
    };
    host == "localhost"
        || host
            .trim_matches(['[', ']'])
            .parse::<IpAddr>()
            .is_ok_and(|address| match address {
                IpAddr::V4(address) => address.is_private() || address.is_loopback(),
                IpAddr::V6(address) => address.is_loopback(),
            })
}

#[cfg(test)]
mod tests {
    use super::provider_error_message;
    use serde_json::json;

    #[test]
    fn structured_output_length_error_is_actionable_without_echoing_server_content() {
        let body = serde_json::to_vec(&json!({"error":{"code":"structured_output_failed","message":"Incomplete at finish_reason=length PRIVATE_SERVER_DATA"}})).unwrap();
        let message = provider_error_message(502, &body);
        assert!(message.contains("plafond de sortie"));
        assert!(!message.contains("PRIVATE_SERVER_DATA"));
        let other = serde_json::to_vec(
            &json!({"error":{"code":"other","message":"length PRIVATE_SERVER_DATA"}}),
        )
        .unwrap();
        assert_eq!(
            provider_error_message(502, &other),
            "Provider HTTP 502; response body withheld"
        );
    }

    #[test]
    fn malformed_or_large_error_bodies_cannot_override_the_bounded_diagnostic() {
        for body in [b"not json".to_vec(),serde_json::to_vec(&json!({"error":{"type":"structured_output_failed","message":"length","private":"x".repeat(8192)}})).unwrap()] {
            assert_eq!(provider_error_message(502,&body),"Provider HTTP 502; response body withheld");
        }
    }
}
