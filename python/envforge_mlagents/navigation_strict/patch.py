from typing import Any

from mlagents.torch_utils import torch
from mlagents.trainers.torch_entities import networks
from mlagents.trainers.torch_entities.action_model import ActionModel
from mlagents.trainers.torch_entities.agent_action import AgentAction
from mlagents.trainers.torch_entities.networks import SimpleActor
from mlagents.trainers.torch_entities.utils import ModelUtils
from mlagents_envs.base_env import ActionSpec, ActionTuple

from envforge_mlagents.navigation_strict.network import StrictNavigationNetworkBody

_ORIGINAL_SIMPLE_ACTOR_INIT = SimpleActor.__init__
_ORIGINAL_SIMPLE_ACTOR_GET_ACTION_AND_STATS = SimpleActor.get_action_and_stats
_MODEL_REPORT_PRINTED = False


def apply_navigation_strict_patch() -> None:
    networks.NetworkBody = StrictNavigationNetworkBody
    SimpleActor.__init__ = _strict_simple_actor_init
    SimpleActor.get_action_and_stats = _strict_get_action_and_stats
    print(
        "EnvForge strict patch: "
        "networks.NetworkBody is StrictNavigationNetworkBody -> "
        f"{networks.NetworkBody is StrictNavigationNetworkBody}"
    )


def _strict_simple_actor_init(self: SimpleActor, *args: Any, **kwargs: Any) -> None:
    observation_specs = kwargs.get("observation_specs", args[0] if args else None)
    network_settings = kwargs.get(
        "network_settings",
        args[1] if len(args) > 1 else None,
    )
    action_spec = kwargs.get("action_spec", args[2] if len(args) > 2 else None)
    conditional_sigma = kwargs.get(
        "conditional_sigma",
        args[3] if len(args) > 3 else False,
    )
    tanh_squash = kwargs.get("tanh_squash", args[4] if len(args) > 4 else False)
    _ORIGINAL_SIMPLE_ACTOR_INIT(self, *args, **kwargs)
    if isinstance(self.network_body, StrictNavigationNetworkBody):
        self.action_model = StrictNavigationActionModel(
            self.encoding_size,
            action_spec,
            conditional_sigma=conditional_sigma,
            tanh_squash=tanh_squash,
            deterministic=network_settings.deterministic,
        )
    _print_model_report_once(self, observation_specs)


def _strict_get_action_and_stats(
    self: SimpleActor,
    inputs: list[torch.Tensor],
    masks: torch.Tensor | None = None,
    memories: torch.Tensor | None = None,
    sequence_length: int = 1,
) -> Any:
    if not isinstance(self.action_model, StrictNavigationActionModel):
        return _ORIGINAL_SIMPLE_ACTOR_GET_ACTION_AND_STATS(
            self,
            inputs,
            masks=masks,
            memories=memories,
            sequence_length=sequence_length,
        )

    encoding, memories = self.network_body(
        inputs,
        memories=memories,
        sequence_length=sequence_length,
    )
    action, log_probs, entropies = self.action_model(encoding, masks)
    run_out = {
        "env_action": self.action_model.to_env_action_tuple(action),
        "log_probs": log_probs,
        "entropy": entropies,
    }
    return action, run_out, memories


def _print_model_report_once(actor: SimpleActor, observation_specs: Any) -> None:
    global _MODEL_REPORT_PRINTED
    if _MODEL_REPORT_PRINTED:
        return

    _MODEL_REPORT_PRINTED = True
    network_body = actor.network_body
    print("EnvForge strict model report:")
    print(
        "- networks.NetworkBody is StrictNavigationNetworkBody: "
        f"{networks.NetworkBody is StrictNavigationNetworkBody}"
    )
    print(
        "- actor.network_body class: "
        f"{network_body.__class__.__module__}.{network_body.__class__.__name__}"
    )
    print(
        "- actor.action_model class: "
        f"{actor.action_model.__class__.__module__}."
        f"{actor.action_model.__class__.__name__}"
    )
    print("- actor.network_body module:")
    print(network_body)

    smoke_inputs = [torch.zeros((1, *spec.shape)) for spec in observation_specs]
    was_image_stats_enabled = getattr(network_body, "image_stats_enabled", False)
    network_body.image_stats_enabled = False
    output, memory = network_body(smoke_inputs)
    network_body.image_stats_enabled = was_image_stats_enabled
    input_shapes = [tuple(input_tensor.shape) for input_tensor in smoke_inputs]
    print(f"- forward smoke input shapes: {input_shapes}")
    print(f"- forward smoke output shape: {tuple(output.shape)}")
    print(f"- forward smoke memory: {memory}")

    action_outputs = actor.action_model.get_action_out(output, masks=None)
    continuous_output = action_outputs[0]
    deterministic_continuous_output = action_outputs[3]
    if continuous_output is not None:
        print(
            "- strict action smoke output: "
            f"{continuous_output.detach().cpu().tolist()}"
        )
    if deterministic_continuous_output is not None:
        print(
            "- strict deterministic action smoke output: "
            f"{deterministic_continuous_output.detach().cpu().tolist()}"
        )


class StrictNavigationActionModel(ActionModel):
    def __init__(
        self,
        hidden_size: int,
        action_spec: ActionSpec,
        conditional_sigma: bool = False,
        tanh_squash: bool = False,
        deterministic: bool = False,
    ):
        if action_spec.continuous_size != 2 or action_spec.discrete_size != 0:
            raise ValueError(
                "Strict navigation expects exactly two continuous actions."
            )

        super().__init__(
            hidden_size,
            action_spec,
            conditional_sigma=conditional_sigma,
            tanh_squash=tanh_squash,
            deterministic=deterministic,
        )

    def to_env_action_tuple(self, actions: AgentAction) -> ActionTuple:
        action_tuple = ActionTuple()
        continuous_tensor = actions.continuous_tensor
        if continuous_tensor is not None:
            action_tuple.add_continuous(
                ModelUtils.to_numpy(self._map_continuous_actions(continuous_tensor))
            )
        return action_tuple

    def get_action_out(self, inputs: torch.Tensor, masks: torch.Tensor) -> Any:
        dists = self._get_dists(inputs, masks)
        continuous_out, action_out_deprecated = None, None
        deterministic_continuous_out = None

        if self.action_spec.continuous_size > 0 and dists.continuous is not None:
            continuous_out = self._map_continuous_actions(
                dists.continuous.exported_model_output()
            )
            action_out_deprecated = continuous_out
            deterministic_continuous_out = self._map_continuous_actions(
                dists.continuous.deterministic_sample()
            )

        return (
            continuous_out,
            None,
            action_out_deprecated,
            deterministic_continuous_out,
            None,
        )

    @staticmethod
    def _map_continuous_actions(raw_actions: torch.Tensor) -> torch.Tensor:
        forward_speed = torch.sigmoid(raw_actions[:, 0:1])
        angular_velocity = torch.clamp(raw_actions[:, 1:2], -3, 3) / 3
        return torch.cat([forward_speed, angular_velocity], dim=1)
