import os

from mlagents.torch_utils import nn, torch
from mlagents.trainers.buffer import AgentBuffer
from mlagents.trainers.exception import UnityTrainerException
from mlagents.trainers.settings import NetworkSettings
from mlagents.trainers.torch_entities.encoders import VectorInput, conv_output_shape
from mlagents.trainers.torch_entities.layers import Initialization, linear_layer
from mlagents.trainers.trajectory import ObsUtil
from mlagents_envs.base_env import ObservationSpec


class StrictNavigationVisualEncoder(nn.Module):
    def __init__(
        self,
        height: int,
        width: int,
        initial_channels: int,
        output_size: int,
    ):
        super().__init__()
        conv_1_hw = conv_output_shape((height, width), 8, 4)
        conv_2_hw = conv_output_shape(conv_1_hw, 4, 2)
        self.final_flat = conv_2_hw[0] * conv_2_hw[1] * 32

        if (height, width, initial_channels, self.final_flat) != (84, 112, 3, 3456):
            raise UnityTrainerException(
                "Strict navigation expects a visual observation shaped as 3x84x112."
            )

        self.conv_layers = nn.Sequential(
            nn.Conv2d(initial_channels, 16, [8, 8], [4, 4]),
            nn.LeakyReLU(),
            nn.Conv2d(16, 32, [4, 4], [2, 2]),
            nn.LeakyReLU(),
        )
        self.dense = nn.Sequential(
            linear_layer(
                self.final_flat,
                output_size,
                kernel_init=Initialization.KaimingHeNormal,
                kernel_gain=1.41,
            ),
            nn.LeakyReLU(),
        )

    def forward(self, visual_obs: torch.Tensor) -> torch.Tensor:
        hidden = self.conv_layers(visual_obs)
        hidden = hidden.reshape(-1, self.final_flat)
        return self.dense(hidden)


class SigmoidGateLayer(nn.Module):
    def __init__(self, input_size: int, output_size: int):
        super().__init__()
        self.linear = linear_layer(
            input_size,
            output_size,
            kernel_init=Initialization.KaimingHeNormal,
            kernel_gain=1.0,
        )

    def forward(self, inputs: torch.Tensor) -> torch.Tensor:
        hidden = self.linear(inputs)
        return hidden * torch.sigmoid(hidden)


class StrictNavigationNetworkBody(nn.Module):
    def __init__(
        self,
        observation_specs: list[ObservationSpec],
        network_settings: NetworkSettings,
        encoded_act_size: int = 0,
    ):
        super().__init__()
        if network_settings.memory is not None:
            raise UnityTrainerException(
                "Strict navigation does not support recurrent memory."
            )
        if encoded_act_size != 0:
            raise UnityTrainerException(
                "Strict navigation does not support action-conditioned network bodies."
            )

        self.normalize = network_settings.normalize
        self.use_lstm = False
        self.h_size = network_settings.hidden_units
        self.m_size = 0
        self.image_stats_enabled = True
        self._forward_count = 0
        self._image_stats_interval = _read_positive_int(
            "ENVFORGE_IMAGE_STATS_INTERVAL",
            1000,
        )

        visual_spec, vector_spec = self._resolve_observation_specs(observation_specs)
        channels, height, width = visual_spec.shape
        vector_size = vector_spec.shape[0]
        if self.h_size != 256 or vector_size != 2:
            raise UnityTrainerException(
                "Strict navigation expects hidden_units=256 and a 2-value vector "
                "observation."
            )

        self.visual_encoder = StrictNavigationVisualEncoder(
            height,
            width,
            channels,
            self.h_size,
        )
        self.vector_input = VectorInput(vector_size, self.normalize)
        self.processors = nn.ModuleList([self.visual_encoder, self.vector_input])
        self._visual_index = observation_specs.index(visual_spec)
        self._vector_index = observation_specs.index(vector_spec)
        self._body_encoder = nn.Sequential(
            SigmoidGateLayer(self.h_size + vector_size, self.h_size),
            SigmoidGateLayer(self.h_size, self.h_size),
        )

    @staticmethod
    def _resolve_observation_specs(
        observation_specs: list[ObservationSpec],
    ) -> tuple[ObservationSpec, ObservationSpec]:
        visual_specs = [spec for spec in observation_specs if len(spec.shape) == 3]
        vector_specs = [spec for spec in observation_specs if len(spec.shape) == 1]
        if len(visual_specs) != 1 or len(vector_specs) != 1:
            raise UnityTrainerException(
                "Strict navigation expects exactly one visual observation and one "
                "vector observation."
            )
        return visual_specs[0], vector_specs[0]

    def update_normalization(self, buffer: AgentBuffer) -> None:
        if self.vector_input.normalizer is None:
            return
        obs = ObsUtil.from_buffer(buffer, len(self.processors))
        self.vector_input.update_normalization(
            torch.as_tensor(obs[self._vector_index].to_ndarray())
        )

    def copy_normalization(self, other_network: "StrictNavigationNetworkBody") -> None:
        self.vector_input.copy_normalization(other_network.vector_input)

    @property
    def memory_size(self) -> int:
        return 0

    def forward(
        self,
        inputs: list[torch.Tensor],
        actions: torch.Tensor | None = None,
        memories: torch.Tensor | None = None,
        sequence_length: int = 1,
    ) -> tuple[torch.Tensor, torch.Tensor]:
        if actions is not None:
            raise UnityTrainerException(
                "Strict navigation does not support action-conditioned forward passes."
            )

        visual_input = inputs[self._visual_index]
        self._log_image_stats(visual_input)

        image_features = self.visual_encoder(visual_input)
        numeric_features = self.vector_input(inputs[self._vector_index])
        encoding = self._body_encoder(
            torch.cat([image_features, numeric_features], dim=1)
        )
        return encoding, memories

    def _log_image_stats(self, visual_input: torch.Tensor) -> None:
        if not self.image_stats_enabled:
            return

        self._forward_count += 1
        if (
            self._forward_count != 1
            and self._forward_count % self._image_stats_interval != 0
        ):
            return

        with torch.no_grad():
            detached = visual_input.detach()
            means = detached.mean(dim=(0, 2, 3)).cpu().tolist()
            min_value = detached.min().item()
            max_value = detached.max().item()

        print(
            "EnvForge strict image stats: "
            f"forward={self._forward_count}, "
            f"shape={tuple(visual_input.shape)}, "
            f"mean_rgb=({means[0]:.4f}, {means[1]:.4f}, {means[2]:.4f}), "
            f"min={min_value:.4f}, max={max_value:.4f}"
        )


def _read_positive_int(name: str, default: int) -> int:
    raw_value = os.environ.get(name)
    if raw_value is None:
        return default

    try:
        parsed_value = int(raw_value)
    except ValueError:
        return default

    return parsed_value if parsed_value > 0 else default
